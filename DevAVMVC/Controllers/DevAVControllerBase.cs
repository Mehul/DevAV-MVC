using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace DevAVMVC.Controllers
{
public abstract class DevAVControllerBase : Controller {
        const string
            SelectedItemIDKey = "SelectedItemID",
            SearchTextKey = "SearchText";

        public abstract string ControllerName { get; }
        public abstract bool ShowSearchBox { get; }
        protected abstract int DefaultSelectedItemID { get; }


        protected virtual void EnsureViewBagInfo() {
            ViewBag.Title = string.Format("{0} - DevAV Demo | ASP.NET Controls by DevExpress", ControllerName);
            ViewBag.PageLogoImageUrl = string.Format("/Content/Images/LogoMenuIcons/{0}.png", ControllerName);
            ViewBag.ToolbarMenuXPath = string.Format("Pages/{0}/Item", ControllerName);
            ViewBag.ShowSearchBox = ShowSearchBox;
            ViewBag.IsGridView = DemoUtils.IsEmployeeGridViewMode; //Should be virtual or abstract. 
            ViewBag.ControllerName = ControllerName;
            ViewBag.SearchText = SearchText;
        }

        
        protected override void OnActionExecuting(ActionExecutingContext filterContext) {
            EnsureViewBagInfo();
            base.OnActionExecuting(filterContext);
        }

        // necessary for views that have a masterdetail
        protected int SelectedItemID { 
            get { 
                var id = Request.Params[SelectedItemIDKey];
                if(string.IsNullOrEmpty(id))
                    return DefaultSelectedItemID;
                return Convert.ToInt32(id);
            } 
        }
        protected string SearchText { get { return Request != null ? Request.Params[SearchTextKey] : string.Empty; } }
    }
}