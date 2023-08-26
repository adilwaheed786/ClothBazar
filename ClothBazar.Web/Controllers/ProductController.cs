using ClothBazar.Entities;
using ClothBazar.Services;
using ClothBazar.Web.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace ClothBazar.Web.Controllers
{
    public class ProductController : Controller
    {
        // GET: Product
        [Authorize(Roles ="Admin")]
        public ActionResult Index()
        {
            return View();
        }
        [AllowAnonymous]
        public ActionResult ProductTable(string search, int? pageNo)
        {
            var pageSize = ConfigurationsService.Instance.PageSize();

            ProductSearchViewModel model = new ProductSearchViewModel();
            model.SearchTerm = search;

            pageNo = pageNo.HasValue ? pageNo.Value > 0 ? pageNo.Value : 1 : 1;
            
            var totalRecords = ProductsService.Instance.GetProductsCount(search);
            model.Products = ProductsService.Instance.GetProducts(search, pageNo.Value, pageSize);

            model.Pager = new Pager(totalRecords, pageNo, pageSize);

            return PartialView(model);
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public ActionResult Create()
        {
            NewProductViewModel model = new NewProductViewModel();
            
            model.AvailableCategories = CategoriesService.Instance.GetAllCategories();
            
            return PartialView(model);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public ActionResult Create(NewProductViewModel model)
        {
            var newProduct = new Product();
            newProduct.Name = model.Name;
            newProduct.Description = model.Description;
            newProduct.Price = model.Price;
            newProduct.Category = CategoriesService.Instance.GetCategory(model.CategoryID);
            newProduct.ImageURL = model.ImageURL;
            newProduct.Quantity = model.Quantity;
            ProductsService.Instance.SaveProduct(newProduct);
            return RedirectToAction("ProductTable");            
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public ActionResult Edit(int ID)
        {
            EditProductViewModel model = new EditProductViewModel();
            
            var product = ProductsService.Instance.GetProduct(ID);

            model.ID = product.ID;
            model.Name = product.Name;
            model.Description = product.Description;
            model.Price = product.Price;
            model.CategoryID = product.Category != null ? product.Category.ID : 0;
            model.ImageURL = product.ImageURL;
            model.Quantity = product.Quantity;
            model.AvailableCategories = CategoriesService.Instance.GetAllCategories();

            return PartialView(model);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public ActionResult Edit(EditProductViewModel model)
        {
            var existingProduct = ProductsService.Instance.GetProduct(model.ID);
            existingProduct.Name = model.Name;
            existingProduct.Description = model.Description;
            existingProduct.Price = model.Price;
            existingProduct.Quantity=model.Quantity;
            existingProduct.Category = null; //mark it null. Because the referncy key is changed below
            existingProduct.CategoryID = model.CategoryID;

            //dont update imageURL if its empty
            if (!string.IsNullOrEmpty(model.ImageURL))
            {
                existingProduct.ImageURL = model.ImageURL;
            }

            ProductsService.Instance.UpdateProduct(existingProduct);

            return RedirectToAction("ProductTable");
        }
        
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public ActionResult Delete(int ID)
        {
            ProductsService.Instance.DeleteProduct(ID);

            return RedirectToAction("ProductTable");
        }
        
        [HttpGet]
        [AllowAnonymous]
        public ActionResult Details(int ID)
        { 
            ProductViewModel model = new ProductViewModel();

            model.Product = ProductsService.Instance.GetProduct(ID);

            if (model.Product == null)  return View("PageNotFound"); ;

            return View(model);
        }
    }
}