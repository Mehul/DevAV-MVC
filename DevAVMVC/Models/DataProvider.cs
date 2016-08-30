using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using DevExpress.Data.Filtering;
using DevExpress.Data.Linq;
using DevExpress.Data.Linq.Helpers;
using DevExpress.DevAV;
using DevExpress.Internal;

public static class DataProvider {
    static bool useCache = true;
    static string databaseVersion;
    static List<CustomerStore> customerStores;

    static List<DataEntryInfo> oppChartData;
    static List<DataEntryInfo> rChartData;
    static Dictionary<long, List<DataEntryInfo>> productSalesChartData;
    static Dictionary<long, List<DataEntryInfo>> productOppChartData;

    static HttpContext Context { get { return HttpContext.Current; } }
    static DevAVDb DataContext {
        get {
            var storageKey = "C47520FF-2FFA-4468-9C22-E6BD4C2FC0C0";
            if(Context == null)
                return GetDataContext();
            if(!Context.Items.Contains(storageKey))
                Context.Items[storageKey] = GetDataContext();
            return (DevAVDb)Context.Items[storageKey];
        }
    }
    static ICriteriaToExpressionConverter ExpressionConverter { get { return new CriteriaToEFExpressionConverter(); } }

    public static long emptyEntryID = -1;
    public static string DatabaseVersion {
        get {
            //if(string.IsNullOrEmpty(databaseVersion))
            //    databaseVersion = DataContext.Version.First().Date.ToString("dd_MM_yyyy-HH_mm_ss");

            databaseVersion = "0";
            return databaseVersion;
        }
    }

    public static IQueryable<Employee> Employees { get { return DataContext.Employees; } }
    public static IQueryable<EmployeeTask> EmployeeTasks { get { return DataContext.Tasks.Where(t => t.AssignedEmployeeId != null && t.OwnerId != null); } }
    public static IQueryable<Customer> Customers { get { return DataContext.Customers; } }
    public static IQueryable<Product> Products { get { return DataContext.Products.Where(p => p.EngineerId != null && p.SupportId != null); } }
    public static IQueryable<CustomerEmployee> CustomerEmployees { get { return DataContext.CustomerEmployees; } }
    public static IQueryable<Evaluation> Evaluations { get { return DataContext.Evaluations; } }
    public static IQueryable<State> States { get { return DataContext.States; } }
    public static IQueryable<Crest> Crests { get { return DataContext.Crests; } }
    public static IQueryable<Order> Orders { get { return DataContext.Orders; } }
    public static IQueryable<OrderItem> OrderItems { get { return DataContext.OrderItems.Where(i => i.Order.OrderDate < DateTime.Now); } }
    public static IQueryable<QuoteItem> QuoteItems { get { return DataContext.QuoteItems.Where(i => i.Quote.Date < DateTime.Now); } }
    public static IQueryable<CustomerStore> CustomerStores { get { return DataContext.CustomerStores; } }
    public static IQueryable<Quote> Quotes { get { return DataContext.Quotes.Where(q => q.Date < DateTime.Now); } }

    public static IQueryable<Employee> GetEmployees(string filterExpression) {
        return (IQueryable<Employee>)Employees.AppendWhere(ExpressionConverter, CriteriaOperator.Parse(filterExpression));
    }
    public static IQueryable<EmployeeTask> GetEmployeeTasks(string filterExpression) {
        return (IQueryable<EmployeeTask>)EmployeeTasks.AppendWhere(ExpressionConverter, CriteriaOperator.Parse(filterExpression));
    }

    public static Employee CreateEmployee() {
        return DataContext.Employees.Add(DataContext.Employees.Create());
    }
    public static EmployeeTask CreateTask() {
        return DataContext.Tasks.Add(DataContext.Tasks.Create());
    }
    public static void DeleteEmployee(long id) {
        var employee = Employees.FirstOrDefault(e => e.Id == id);
        if(employee == null)
            return;
        DeleteEmployeeRelations(id);
        DataContext.Employees.Remove(employee);
        DataContext.SaveChanges();
    }
    public static void DeleteTask(long id) {
        var task = EmployeeTasks.FirstOrDefault(e => e.Id == id);
        if(task == null)
            return;
        DataContext.Tasks.Remove(task);
        DataContext.SaveChanges();
    }
    public static void DeleteEvaluation(long id) {
        var evaluation = Evaluations.FirstOrDefault(e => e.Id == id);
        if(evaluation == null)
            return;
        DataContext.Evaluations.Remove(evaluation);
        DataContext.SaveChanges();
    }
    public static ProductImage CreateProductImage(Product product, byte[] data) {
        var image = DataContext.ProductImages.Create();
        image.Picture = new Picture();
        image.Picture.Data = data;
        product.Images.Add(image);
        SaveChanges();
        return image;
    }

    public static void SaveChanges() {
        DataContext.SaveChanges();
    }

    public static List<CustomerStore> GetCustomerStores() {
        if(useCache || customerStores == null) // TODO
            customerStores = CustomerStores.ToList();
        return customerStores;
    }
    public static List<DataEntryInfo> GetRevenueChartData() {
        if(!useCache || rChartData == null)
            rChartData = OrderItems.GroupBy(i => i.Product.Category).Select(g => new DataEntryInfo() { Name = g.Key.ToString(), Value = g.Sum(i => i.Total) }).ToList();
        return rChartData;
    }
    public static List<DataEntryInfo> GetOpportunitiesChartData() {
        if(!useCache || oppChartData == null) {
            oppChartData = new List<DataEntryInfo>() { 
                new DataEntryInfo() { Name = "High",        Value = Quotes.Where(q => q.Opportunity > 0.6).Sum(q => q.Total) },
                new DataEntryInfo() { Name = "Medium",      Value = Quotes.Where(q => q.Opportunity > 0.3 && q.Opportunity < 0.6).Sum(q => q.Total) },
                new DataEntryInfo() { Name = "Low",         Value = Quotes.Where(q => q.Opportunity > 0.12 && q.Opportunity < 0.3).Sum(q => q.Total) },
                new DataEntryInfo() { Name = "Unlikely",    Value = Quotes.Where(q => q.Opportunity < 0.12).Sum(q => q.Total) }
            };
        }
        return oppChartData;
    }
    public static List<DataEntryInfo> GetProductOpportunitiesChartData(Product product) {
        if(!useCache || productOppChartData == null)
            productOppChartData = CreateProductOppChartData();
        return productOppChartData[product.Id];
    }
    public static List<DataEntryInfo> GetProductSalesChartData(Product product) {
        if(!useCache || productSalesChartData == null)
            productSalesChartData = CreateProductSalesChartData();
        return productSalesChartData[product.Id];
    }

    static DevAVDb GetDataContext() {
        DevExpress.DemoData.DemoDbContext.IsWebModel = true;

        var connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["DevAVConnectionString"].ConnectionString;
        connectionString = DbEngineDetector.PatchConnectionString(connectionString);
        return new DevAVDb(connectionString);
    }
    static Dictionary<long, List<DataEntryInfo>> CreateProductSalesChartData() {
        return OrderItems.GroupBy(q => q.ProductId)
            .Select(g => new {
                K = g.Key.Value,
                V = g.GroupBy(q => q.Order.OrderDate.Year)
                    .Select(yg => new { Year = yg.Key, Total = yg.Sum(i => i.Total) }).OrderBy(i => i.Year).ToList()
            }).ToDictionary(i => i.K, i => i.V.Select(e => new DataEntryInfo() { Name = e.Year.ToString(), Value = e.Total }).ToList());
    }
    static Dictionary<long, List<DataEntryInfo>> CreateProductOppChartData() {
        return QuoteItems.GroupBy(q => q.ProductId)
            .Select(g => new {
                K = g.Key.Value,
                V = g.GroupBy(q => q.Quote.Date.Year)
                    .Select(yg => new { Year = yg.Key, Total = yg.Sum(i => i.Total) }).OrderBy(i => i.Year).ToList()
            }).ToDictionary(i => i.K, i => i.V.Select(e => new DataEntryInfo() { Name = e.Year.ToString(), Value = e.Total }).ToList());
    }
    static void DeleteEmployeeRelations(long id) {
        var tasks = DataContext.Tasks.Where(task => task.OwnerId == id || task.AssignedEmployeeId == id);
        foreach(var task in tasks) {
            if(task.OwnerId == id)
                task.OwnerId = null;
            if(task.AssignedEmployeeId == null)
                task.AssignedEmployeeId = null;
        }

        var evaluations = DataContext.Evaluations.Where(ev => ev.CreatedById == id || ev.EmployeeId == id);
        foreach(var evaluation in evaluations) {
            if(evaluation.CreatedById == id)
                evaluation.CreatedById = null;
            if(evaluation.EmployeeId == id)
                evaluation.EmployeeId = null;
        }

        var products = DataContext.Products.Where(p => p.EngineerId == id || p.SupportId == id);
        foreach(var product in products) {
            if(product.EngineerId == id)
                product.EngineerId = null;
            if(product.SupportId == id)
                product.SupportId = null;
        }

        var orders = DataContext.Orders.Where(o => o.EmployeeId == id);
        foreach(var order in orders)
            order.EmployeeId = null;

        var quotes = DataContext.Quotes.Where(q => q.EmployeeId == id).ToList();
        foreach(var quote in quotes)
            quote.EmployeeId = null;

        var communications = DataContext.Communications.Where(c => c.EmployeeId == id);
        foreach(var communication in communications)
            communication.EmployeeId = null;
    }
}

public class DataEntryInfo {
    public string Name { get; set; }
    public decimal Value { get; set; }
}
