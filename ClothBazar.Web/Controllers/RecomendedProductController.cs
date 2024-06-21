using ClothBazar.Services;
using ClothBazar.Web.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace ClothBazar.Web.Controllers
{
    public class RecomendedProductController : Controller
    {
        // GET: RecomendedProduct
        public ActionResult Products()
        {
            RecomendedViewModel model = new RecomendedViewModel();
            model.Products = ProductsService.Instance.GetProducts(1, 8);
            return PartialView(model);
        }

    }
}