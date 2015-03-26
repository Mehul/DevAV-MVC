using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using DevExpress.DevAV;

namespace DevAVMVC.Controllers
{
public class EmployeesController : DevAVControllerBase {
        public override string ControllerName { get { return "Employees"; } }
        public override bool ShowSearchBox { get { return true; } }
        protected override int DefaultSelectedItemID { get { return (int)DataProvider.Employees.First().Id; } }

        protected Employee SelectedEmployee { get { return DataProvider.Employees.FirstOrDefault(e => e.Id == SelectedItemID); } }
        protected List<Employee> Employees { get { return DataProvider.Employees.ToList(); } }

        public ActionResult Index() {
            if(Request.IsAjaxRequest()) //IsCallback
                return PartialView();
            return View(); //otherwise load the view that loads the partialview + rootlayout. Like reloading whole page.
        }

        public ActionResult GridView_Master() {
            return PartialView(Employees);
        }

        //why use renderaction vs renderpartial because we need separate models and this way I don't need to make extra views
        //we use different models because gridview uses in memory filtering and cardview does not. So to keep with MVC separate concerns...etc.
        public ActionResult CardView_Master() {
            var searchCriteria = FilterBag.GetSearchCriteria(SearchText, "FirstName", "LastName");
            var employees = DataProvider.GetEmployees(!object.ReferenceEquals(searchCriteria, null) ? searchCriteria.ToString() : string.Empty).ToList();
            return PartialView(employees);
        }

        public ActionResult GridView_Detail() {
            return PartialView(SelectedEmployee);
        }

        public ActionResult CardView_Detail() {
            return PartialView(SelectedEmployee);
        }

        public ActionResult DetailCallbackPanel() {
            return PartialView();
        }

        public ActionResult GridView_EvaluationsGridView() {
            var employee = SelectedEmployee;
            return PartialView(employee != null ? employee.Evaluations : null);
        }

        public ActionResult GridView_TasksGridView() {
            var employee = SelectedEmployee;
            return PartialView(employee != null ? employee.AssignedTasks : null);
        }

        public ActionResult CardView_EvaluationsGridView() {
            var employee = SelectedEmployee;
            return PartialView(employee != null ? employee.Evaluations : null);
        }

        public ActionResult CardView_TasksGridView() {
            var employee = SelectedEmployee;
            return PartialView(employee != null ? employee.AssignedTasks : null);
        }

    }
}