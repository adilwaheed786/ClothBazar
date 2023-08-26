using ClothBazar.Entities;
using ClothBazar.Services;
using ClothBazar.Web.Code;
using ClothBazar.Web.ViewModels;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Web;
using System.Web.Mvc;
using Stripe;
using Stripe.Checkout;
using System.Web.Configuration;
using System.Configuration;

namespace ClothBazar.Web.Controllers
{
    public class ShopController : Controller
    {
        private ApplicationSignInManager _signInManager;
        private ApplicationUserManager _userManager;

        public ShopController()
        {
            StripeConfiguration.ApiKey = WebConfigurationManager.AppSettings["StripeSecretKey"];
        }
        public ApplicationSignInManager SignInManager
        {
            get
            {
                return _signInManager ?? HttpContext.GetOwinContext().Get<ApplicationSignInManager>();
            }
            private set
            {
                _signInManager = value;
            }
        }
        public ApplicationUserManager UserManager
        {
            get
            {
                return _userManager ?? HttpContext.GetOwinContext().GetUserManager<ApplicationUserManager>();
            }
            private set
            {
                _userManager = value;
            }
        }

        public ActionResult Index(string searchTerm, int? minimumPrice, int? maximumPrice, int? categoryID, int? sortBy, int? pageNo)
        {
            var pageSize = ConfigurationsService.Instance.ShopPageSize();

            ShopViewModel model = new ShopViewModel();

            model.SearchTerm = searchTerm;
            model.FeaturedCategories = CategoriesService.Instance.GetFeaturedCategories();
            model.MaximumPrice = ProductsService.Instance.GetMaximumPrice();

            pageNo = pageNo.HasValue ? pageNo.Value > 0 ? pageNo.Value : 1 : 1;
            model.SortBy = sortBy;
            model.CategoryID = categoryID;

            int totalCount = ProductsService.Instance.SearchProductsCount(searchTerm, minimumPrice, maximumPrice, categoryID, sortBy);
            model.Products = ProductsService.Instance.SearchProducts(searchTerm, minimumPrice, maximumPrice, categoryID, sortBy, pageNo.Value, pageSize);

            model.Pager = new Pager(totalCount, pageNo, pageSize);

            return View(model);
        }

        public ActionResult FilterProducts(string searchTerm, int? minimumPrice, int? maximumPrice, int? categoryID, int? sortBy, int? pageNo)
        {
            var pageSize = ConfigurationsService.Instance.ShopPageSize();

            FilterProductsViewModel model = new FilterProductsViewModel();

            model.SearchTerm = searchTerm;
            pageNo = pageNo.HasValue ? pageNo.Value > 0 ? pageNo.Value : 1 : 1;
            model.SortBy = sortBy;
            model.CategoryID = categoryID;

            int totalCount = ProductsService.Instance.SearchProductsCount(searchTerm, minimumPrice, maximumPrice, categoryID, sortBy);
            model.Products = ProductsService.Instance.SearchProducts(searchTerm, minimumPrice, maximumPrice, categoryID, sortBy, pageNo.Value, pageSize);
            
            model.Pager = new Pager(totalCount, pageNo, pageSize);

            return PartialView(model);
        }

        [Authorize]
        public ActionResult Checkout()
        {
            CheckoutViewModel model = new CheckoutViewModel();

            var CartProductsCookie = Request.Cookies["CartProducts"];

            if(CartProductsCookie != null && !string.IsNullOrEmpty(CartProductsCookie.Value))
            {
                model.CartProductIDs = CartProductsCookie.Value.Split('-').Select(x => int.Parse(x)).ToList();

                model.CartProducts = ProductsService.Instance.GetProducts(model.CartProductIDs);

                model.User = UserManager.FindById(User.Identity.GetUserId());
            }

            return View(model);
        }

        [Authorize]
        public JsonResult PlaceOrder(string productIDs)
        {
            JsonResult result = new JsonResult();
            try
            {
                result.JsonRequestBehavior = JsonRequestBehavior.AllowGet;

                if (!string.IsNullOrEmpty(productIDs))
                {
                    var productQuantities = productIDs.Split('-').Select(x => int.Parse(x)).ToList();

                    var distinctProductIDs = productQuantities.Distinct().ToList();

                    var boughtProducts = ProductsService.Instance.GetProducts(distinctProductIDs);
                    var insufficientQuantities = new List<int>();

                    foreach (var product in boughtProducts)
                    {
                        var quantity = productQuantities.Count(productID => productID == product.ID);

                        if (product.Quantity < quantity)
                        {
                            insufficientQuantities.Add(product.ID);
                        }
                    }

                    if (insufficientQuantities.Any())
                    {
                        var insufficientProducts = boughtProducts
                             .Where(product => insufficientQuantities.Contains(product.ID))
                             .Select(product => new
                             {
                                 ID = product.ID,
                                 Name = product.Name,
                                 InsufficientQuantity = productQuantities.Count(productID => productID == product.ID) - product.Quantity
                             });

                        result.Data = new { Success = false, InsufficientProducts = insufficientProducts };
                    }
                    else
                    {
                        var sessionLineItems = new List<SessionLineItemOptions>();
                        foreach (var product in boughtProducts)
                        {
                            var quantity = productQuantities.Where(productID => productID == product.ID).Count();
                            sessionLineItems.Add(new SessionLineItemOptions
                            {
                                PriceData = new SessionLineItemPriceDataOptions
                                {
                                    UnitAmount = ((long)product.Price) * 100, // Convert to cents
                                    Currency = "usd",
                                    ProductData = new SessionLineItemPriceDataProductDataOptions
                                    {
                                        Name = product.Name,
                                    },
                                },
                                Quantity = quantity,
                            });
                        }

                        var options = new SessionCreateOptions
                        {
                            LineItems = sessionLineItems,
                            Mode = "payment",
                            SuccessUrl = $"http://localhost:61060/shop/success?productIDs={productIDs}", // Include product IDs in the URL
                            CancelUrl = "http://localhost:61060/shop/cancel",
                        };

                        var service = new SessionService();
                        Session session = service.Create(options);
                        result.Data = new { Success = true, Url = session.Url };
                    }
                }
              
            }
            catch (Exception ex)
            {

                result.Data = new { Success = false };
            }
            return result;
        }

        [Authorize]
        public ActionResult Success(string productIDs)
        {
            if (!string.IsNullOrEmpty(productIDs))
            {
                var productQuantities = productIDs.Split('-').Select(x => int.Parse(x)).ToList();
                var boughtProducts = ProductsService.Instance.GetProducts(productQuantities.Distinct().ToList());

                foreach (var product in boughtProducts)
                {
                    var quantity = productQuantities.Where(productID => productID == product.ID).Count();
                    // Update product quantities after placing the order
                    product.Quantity -= quantity;
                    ProductsService.Instance.UpdateProduct(product);
                }

                // Create the order and save data to the database
                Order newOrder = new Order();
                newOrder.UserID = User.Identity.GetUserId();
                newOrder.OrderedAt = DateTime.Now;
                newOrder.Status = "Pending";
                newOrder.TotalAmount = boughtProducts.Sum(x => x.Price * productQuantities.Where(productID => productID == x.ID).Count());

                newOrder.OrderItems = new List<OrderItem>();
                newOrder.OrderItems.AddRange(boughtProducts.Select(x => new OrderItem() { ProductID = x.ID, Quantity = productQuantities.Where(productID => productID == x.ID).Count(), ItemPrice = x.Price }));

                var rowsEffected = ShopService.Instance.SaveOrder(newOrder);

                // Return a view or appropriate response
                return View();
            }

            // If productIDs are invalid or not found, handle accordingly
            return View("Cancel");
        }
        [Authorize]
        public ActionResult Cancel()
        {
            // Logic if needed before rendering the cancel view
            return View(); // Assuming you have a view named "Cancel.cshtml"
        }
    }
}