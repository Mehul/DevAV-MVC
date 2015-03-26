using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.SessionState;
using System.Xml.Linq;
using DevExpress.Data.Filtering;
using DevExpress.DevAV;
using DevExpress.Web;
using DevExpress.XtraPrinting;
using DevExpress.XtraReports.UI;

public enum ReportType {
    CustomerProfile, CustomerContactsDirectory, CustomerSalesDetail, CustomerSalesSummary,
    EmployeeDirectory, EmployeeSummary, EmployeeProfile, EmployeeTaskList,
    ProductOrders, ProductSalesSummary, ProductProfile,
    TaskList, SalesOrdersSummary
}

public class FilterItem {
    public string Name { get; set; }
    public string Expression { get; set; }
    public bool IsCustom { get; set; }
}

public static class DemoUtils {
    const char SerializedStringArraySeparator = '|';
    const string
        StatesFilePath = "~/App_Data/States.xml",
        GridViewViewModeKey = "GridView",
        ContactImageSlideViewModeKey = "Contacts",
        EmployeePageViewModeCookieKey = "EmployeeViewMode",
        TaskPageViewModeCookieKey = "TaskViewMode",
        ImageSliderModeCookieKey = "CustomerImageSliderMode",
        StateHiddenFieldContextKey = "216A8C03-7A8A-4735-8CBB-4C62E0D4D23C",
        SearchExpressionsContextKey = "7063240E-83E6-415E-A399-5F6C917CA385";

    static bool? _isSiteMode;
    static string spreadsheetTempDir;
    static DateTime spreadsheetTempClearTime = DateTime.Now;
    static Dictionary<ReportType, string> reportDisplayNames = new Dictionary<ReportType, string>();
    static Dictionary<string, string> stateDisplayNames;
    static object reportDisplayNamesLockObject = new object();
    static object stateDisplayNamesLockObject = new object();
    static object clearSpreadsheetTempFolderLockObject = new object();

    static Dictionary<ReportType, string> ReportDisplayNames {
        get {
            lock(reportDisplayNamesLockObject) {
                if(reportDisplayNames.Count == 0)
                    PopuplateReportNames(reportDisplayNames);
                return reportDisplayNames;
            }
        }
    }
    static Dictionary<string, string> StateDisplayNames {
        get {
            lock(stateDisplayNamesLockObject) {
                if(stateDisplayNames == null)
                    stateDisplayNames = XDocument.Load(MapPath(StatesFilePath)).Descendants("State").ToDictionary(n => n.Attribute("Name").Value, n => n.Attribute("DisplayName").Value);
                return stateDisplayNames;
            }
        }
    }

    static DemoUtils() {
        RegisterFilterEnums();
        DashboardFilter = new FilterBag("Dashboard", CreateDashboardStandardFilters());
        CustomerFilter = new FilterBag("Customer", CreateCustomerStandardFilters(), "Name", "BillingAddress.Line", "BillingAddress.City");
        EmployeeFilter = new FilterBag("Employee", CreateEmployeeStandardFilters(), "FirstName", "LastName", "Title");
        TaskFilter = new FilterBag("Task", CreateTaskStandardFilters(), "Subject", "Description", "AssignedEmployee.FirstName", "AssignedEmployee.LastName");
        ProductFilter = new FilterBag("Product", CreateProductStandardFilters(), "Name");
    }

    public static DemoImageLoader ImageLoader = new DemoImageLoader();

    public static HttpContext Context { get { return HttpContext.Current; } }
    static string MapPath(string virtualPath) { return Context.Server.MapPath(virtualPath); }

    static ASPxHiddenField StateHiddenField {
        get { return Context.Items[StateHiddenFieldContextKey] as ASPxHiddenField; }
        set { Context.Items[StateHiddenFieldContextKey] = value; }
    }

    public static bool IsSiteMode {
        get {
            if(!_isSiteMode.HasValue) {
                _isSiteMode = ConfigurationManager.AppSettings["SiteMode"].Equals("true", StringComparison.InvariantCultureIgnoreCase);
            }
            return _isSiteMode.Value;
        }
    }

    public static void RegisterStateHiddenField(ASPxHiddenField hf) {
        StateHiddenField = hf;
    }
    public static bool TryGetClientStateValue<T>(string key, out T result) {
        if(!IsStateHiddenFieldContainsKey(key)) {
            result = default(T);
            return false;
        }
        result = (T)StateHiddenField[key];
        return true;
    }
    public static bool TrySetClientStateValue<T>(string key, T value) {
        if(StateHiddenField == null)
            return false;
        StateHiddenField[key] = value;
        return true;
    }
    public static bool TryGetClientStateIDValue(string key, out long result) {
        if(!IsStateHiddenFieldContainsKey(key)) {
            result = -1;
            return false;
        }
        result = long.Parse(StateHiddenField[key].ToString());
        return true;
    }
    static bool IsStateHiddenFieldContainsKey(string key) {
        return StateHiddenField != null && StateHiddenField.Contains(key);
    }

    // ViewMode
    public static bool IsEmployeeGridViewMode { get { return GetCookie(EmployeePageViewModeCookieKey, GridViewViewModeKey) == GridViewViewModeKey; } }
    public static bool IsTaskGridViewMode { get { return GetCookie(TaskPageViewModeCookieKey, GridViewViewModeKey) == GridViewViewModeKey; } }
    public static bool IsContactImageSliderMode { get { return GetCookie(ImageSliderModeCookieKey, ContactImageSlideViewModeKey) == ContactImageSlideViewModeKey; } }

    static string GetCookie(string key, string defaultValue) {
        var cookie = HttpContext.Current.Request.Cookies[key];
        return cookie == null ? defaultValue : cookie.Value;
    }

    // Search
    public static HtmlString HighlightSearchText(string source, string searchText) { 
        return new HtmlString(HighlightSearchTextCore(source, searchText));
    }

    public static string HighlightSearchTextCore(string source, string searchText) {
        if(string.IsNullOrWhiteSpace(searchText))
            return source;
        var regex = GetSearchExpression(searchText);
        if(regex.IsMatch(source))
            return string.Format("<span>{0}</span>", regex.Replace(source, "<span class='hgl'>$0</span>"));
        return source;
    }
    static Regex GetSearchExpression(string searchText) {
        if(!SearchExpressions.ContainsKey(searchText))
            SearchExpressions[searchText] = new Regex(Regex.Escape(searchText), RegexOptions.IgnoreCase);
        return SearchExpressions[searchText];
    }
    static Dictionary<string, Regex> SearchExpressions {
        get {
            if(Context.Items[SearchExpressionsContextKey] == null)
                Context.Items[SearchExpressionsContextKey] = new Dictionary<string, Regex>();
            return (Dictionary<string, Regex>)Context.Items[SearchExpressionsContextKey];
        }
    }

    // Filters
    public static FilterBag DashboardFilter { get; private set; }
    public static FilterBag CustomerFilter { get; private set; }
    public static FilterBag EmployeeFilter { get; private set; }
    public static FilterBag TaskFilter { get; private set; }
    public static FilterBag ProductFilter { get; private set; }

    static void RegisterFilterEnums() {
        EnumProcessingHelper.RegisterEnum(typeof(CustomerStatus));
        EnumProcessingHelper.RegisterEnum(typeof(EmployeeStatus));
        EnumProcessingHelper.RegisterEnum(typeof(EmployeeTaskStatus));
        EnumProcessingHelper.RegisterEnum(typeof(EmployeeTaskPriority));
    }
    static Dictionary<string, FilterItem> CreateDashboardStandardFilters() {
        var result = new Dictionary<string, FilterItem>();
        var category = new OperandProperty("Product.Category");
        var name = new OperandProperty("Product.Name");
        result.CreateItem("All", null);
        result.CreateItem("Video Players", (CriteriaOperator)(category == "VideoPlayers"));
        result.CreateItem("Plasma TVs", category == "Televisions" & new FunctionOperator(FunctionOperatorType.Contains, name, "Plasma"));
        result.CreateItem("LCD TVs", category == "Televisions" & new FunctionOperator(FunctionOperatorType.Contains, name, "LCD"));
        return result;
    }
    static Dictionary<string, FilterItem> CreateCustomerStandardFilters() {
        var result = new Dictionary<string, FilterItem>();
        var state = new OperandProperty("BillingAddress.State");
        result.CreateItem("All", null);
        result.CreateItem("Illinois", (CriteriaOperator)(state == "IL"));
        result.CreateItem("California", (CriteriaOperator)(state == "CA"));
        result.CreateItem("Arizona", (CriteriaOperator)(state == "AR"));
        result.CreateItem("Georgia", (CriteriaOperator)(state == "GA"));
        return result;
    }
    static Dictionary<string, FilterItem> CreateEmployeeStandardFilters() {
        var result = new Dictionary<string, FilterItem>();
        var status = new OperandProperty("Status");
        result.CreateItem("All", null);
        result.CreateItem("Salaried", (CriteriaOperator)(status == new OperandValue(EmployeeStatus.Salaried)));
        result.CreateItem("Commission", (CriteriaOperator)(status == new OperandValue(EmployeeStatus.Commission)));
        result.CreateItem("Contract", (CriteriaOperator)(status == new OperandValue(EmployeeStatus.Contract)));
        return result;
    }
    static Dictionary<string, FilterItem> CreateTaskStandardFilters() {
        var result = new Dictionary<string, FilterItem>();
        var date = new OperandProperty("DueDate");
        var status = new OperandProperty("Status");
        result.CreateItem("All", null);
        result.CreateItem("Pending", date <= DateTime.Now.Date & status != new OperandValue(EmployeeTaskStatus.Completed));
        result.CreateItem("Deferred", (CriteriaOperator)(status == new OperandValue(EmployeeTaskStatus.Deferred)));
        result.CreateItem("Completed", (CriteriaOperator)(status == new OperandValue(EmployeeTaskStatus.Completed)));
        return result;
    }
    static Dictionary<string, FilterItem> CreateProductStandardFilters() {
        var result = new Dictionary<string, FilterItem>();
        var category = new OperandProperty("Category");
        var name = new OperandProperty("Name");
        result.CreateItem("All", null);
        result.CreateItem("Video Players", (CriteriaOperator)(category == "VideoPlayers"));
        result.CreateItem("Plasma TVs", category == "Televisions" & new FunctionOperator(FunctionOperatorType.Contains, name, "Plasma"));
        result.CreateItem("LCD TVs", category == "Televisions" & new FunctionOperator(FunctionOperatorType.Contains, name, "LCD"));
        return result;
    }

    // Reports
    //public static XtraReport CreateReport(string queryString) {
    //    var args = DemoUtils.DeserializeCallbackArgs(queryString);
    //    if(args.Count == 0)
    //        return null;
    //    var rType = (ReportType)Enum.Parse(typeof(ReportType), args[0]);
    //    var itemID = !string.IsNullOrEmpty(args[1]) ? long.Parse(args[1]) : DataProvider.emptyEntryID;
    //    return CreateReport(rType, itemID);
    //}

    //public static XtraReport CreateReport(ReportType reportType, long itemID) {
    //    switch(reportType) {
    //        case ReportType.CustomerProfile:
    //            return CreateCustomerProfileReport();
    //        case ReportType.CustomerContactsDirectory:
    //            return CreateCustomerContactsReport(itemID);
    //        case ReportType.CustomerSalesDetail:
    //            return CreateCustomerSalesDetailReport(itemID);
    //        case ReportType.CustomerSalesSummary:
    //            return CreateCustomerSalesSummaryReport(itemID);
    //        case ReportType.EmployeeDirectory:
    //            return CreateEmployeeDirectoryReport();
    //        case ReportType.EmployeeSummary:
    //            return CreateEmployeeSummaryReport();
    //        case ReportType.EmployeeProfile:
    //            return CreateEmployeeProfileReport();
    //        case ReportType.EmployeeTaskList:
    //            return CreateEmployeeTaskListReport(itemID);
    //        case ReportType.ProductOrders:
    //            return CreateProductOrdersReport(itemID);
    //        case ReportType.ProductSalesSummary:
    //            return CreateProductSalesSummaryReport(itemID);
    //        case ReportType.ProductProfile:
    //            return CreateProductProfileReport(itemID);
    //        case ReportType.TaskList:
    //            return CreateEmployeeTaskListReport();
    //        case ReportType.SalesOrdersSummary:
    //            return CreateSalesOrdersSummaryReport();
    //    }
    //    return null;
    //}

    //static XtraReport CreateCustomerProfileReport() {
    //    return new CustomerProfile() { DataSource = DataProvider.Customers.ToList() };
    //}
    //static XtraReport CreateCustomerContactsReport(long customerID) {
    //    var report = new CustomerContactsDirectory();
    //    var customer = DataProvider.Customers.FirstOrDefault(c => c.Id == customerID);
    //    if(customer != null)
    //        report.DataSource = customer.Employees;
    //    return report;
    //}
    //static XtraReport CreateCustomerSalesDetailReport(long customerID) {
    //    var report = new CustomerSalesDetailReport();
    //    var customer = DataProvider.Customers.FirstOrDefault(c => c.Id == customerID);
    //    if(customer != null) {
    //        report.SetChartData(QueriesHelper.GetCustomerSaleOrderItemDetails(customer.Id, DataProvider.OrderItems));
    //        report.DataSource = QueriesHelper.GetCustomerSaleDetails(customer.Id, DataProvider.OrderItems);
    //    }
    //    return report;
    //}
    //static XtraReport CreateCustomerSalesSummaryReport(long customerID) {
    //    var report = new CustomerSalesSummaryReport();
    //    var customer = DataProvider.Customers.FirstOrDefault(c => c.Id == customerID);
    //    if(customer != null)
    //        report.DataSource = QueriesHelper.GetCustomerSaleOrderItemDetails(customer.Id, DataProvider.OrderItems);
    //    return report;
    //}
    //static XtraReport CreateEmployeeDirectoryReport() {
    //    return new EmployeeDirectory() { DataSource = DataProvider.Employees.ToList() };
    //}
    //static XtraReport CreateEmployeeSummaryReport() {
    //    return new EmployeeSummary() { DataSource = DataProvider.Employees.ToList() };
    //}
    //static XtraReport CreateEmployeeProfileReport() {
    //    return new EmployeeProfile() { DataSource = DataProvider.Employees.ToList() };
    //}
    //static XtraReport CreateEmployeeTaskListReport(long employeeID) {
    //    var report = new EmployeeTaskList();
    //    var employee = DataProvider.Employees.FirstOrDefault(e => e.Id == employeeID);
    //    if(employee != null)
    //        report.DataSource = employee.AssignedTasks;
    //    return report;
    //}
    //static XtraReport CreateProductOrdersReport(long productID) {
    //    var report = new ProductOrders();
    //    var product = DataProvider.Products.FirstOrDefault(p => p.Id == productID);
    //    if(product != null)
    //        report.DataSource = product.OrderItems;
    //    report.SetStates(DataProvider.States.ToList());
    //    return report;
    //}
    //static XtraReport CreateProductSalesSummaryReport(long productID) {
    //    var report = new ProductSalesSummary();
    //    var product = DataProvider.Products.FirstOrDefault(p => p.Id == productID);
    //    if(product != null)
    //        report.DataSource = product.OrderItems;
    //    return report;
    //}
    //static XtraReport CreateProductProfileReport(long productID) {
    //    return new ProductProfile() { DataSource = DataProvider.Products.Where(p => p.Id == productID).ToList() };
    //}
    //static XtraReport CreateEmployeeTaskListReport() {
    //    return new EmployeeTaskList() { DataSource = DataProvider.EmployeeTasks.ToList() };
    //}
    //static XtraReport CreateSalesOrdersSummaryReport() {
    //    return new SalesOrdersSummaryReport() { DataSource = QueriesHelper.GetSaleSummaries(DataProvider.OrderItems) };
    //}
    
    public static string GetReportDisplayName(ReportType rType) {
        return ReportDisplayNames[rType];
    }
    static void PopuplateReportNames(Dictionary<ReportType, string> names) {
        names[ReportType.CustomerProfile] = "Profile Report";
        names[ReportType.CustomerContactsDirectory] = "Contact Directory Report";
        names[ReportType.CustomerSalesSummary] = "Sales Summary Report";
        names[ReportType.CustomerSalesDetail] = "Sales Detail Report";

        names[ReportType.EmployeeDirectory] = "Directory Report";
        names[ReportType.EmployeeSummary] = "List Report";
        names[ReportType.EmployeeProfile] = "Detail Report";
        names[ReportType.EmployeeTaskList] = "Task Report";

        names[ReportType.ProductOrders] = "Order Details Report";
        names[ReportType.ProductSalesSummary] = "Sales Summary Report";
        names[ReportType.ProductProfile] = "Specification Summary Report";

        names[ReportType.TaskList] = "Task Report";

        names[ReportType.SalesOrdersSummary] = "Sales Orders Summary Report";
    }

    // Spreadsheet
    public static void LoadSpreadsheetFile(Action<string> load) {
        var path = Path.Combine(SpreadsheetTempDir, Guid.NewGuid().ToString() + ".xlsx");
        load(path);
        File.Delete(path);
    }
    public static void ClearSpreadsheetTempFolder() {
        if(DateTime.Now > spreadsheetTempClearTime)
            return;
        lock(clearSpreadsheetTempFolderLockObject) {
            var dir = new DirectoryInfo(SpreadsheetTempDir);
            var files = dir.GetFiles().Where(f => (DateTime.Now - f.LastAccessTime).TotalMinutes > 20);
            foreach(var file in files)
                File.Delete(file.FullName);
            spreadsheetTempClearTime = DateTime.Now.AddMinutes(10);
        }
    }
    static string SpreadsheetTempDir {
        get {
            if(string.IsNullOrEmpty(spreadsheetTempDir)) {
                spreadsheetTempDir = HttpContext.Current.Request.MapPath("~/App_Data/SpreadhsheetTempFolder");
                if(!Directory.Exists(spreadsheetTempDir))
                    Directory.CreateDirectory(spreadsheetTempDir);
            }
            return spreadsheetTempDir;
        }
    }

    public static string GetStateDisplayName(string name) {
        return StateDisplayNames.ContainsKey(name) ? StateDisplayNames[name] : string.Empty;
    }

    public static void BindComboBoxToEnum(ASPxComboBox comboBox, Type enumType) {
        comboBox.ValueType = enumType;
        PopulateComboBoxItems(comboBox.Items, enumType);
    }
    public static void BindComboBoxToEnum(ComboBoxProperties prop, Type enumType) {
        prop.ValueType = enumType;
        PopulateComboBoxItems(prop.Items, enumType);
    }
    public static void EnsureGridFocusedRowIndex(ASPxGridView grid) { // TODO
        grid.FocusedRowIndex = grid.FocusedRowIndex < 0 ? 0 : grid.FocusedRowIndex;
    }

    public static List<string> DeserializeCallbackArgs(string data) {
        List<string> items = new List<string>();
        if(!string.IsNullOrEmpty(data)) {
            int currentPos = 0;
            int dataLength = data.Length;
            while(currentPos < dataLength) {
                string item = DeserializeStringArrayItem(data, ref currentPos);
                items.Add(item);
            }
        }
        return items;
    }
    static string DeserializeStringArrayItem(string data, ref int currentPos) {
        int indexOfFirstSeparator = data.IndexOf(SerializedStringArraySeparator, currentPos);
        string itemLengthString = data.Substring(currentPos, indexOfFirstSeparator - currentPos);
        int itemLength = Int32.Parse(itemLengthString);
        currentPos += itemLengthString.Length + 1;
        string item = data.Substring(currentPos, itemLength);
        currentPos += itemLength;
        return item;
    }
    static void PopulateComboBoxItems(ListEditItemCollection items, Type enumType) {
        items.Clear();
        foreach(var value in Enum.GetValues(enumType))
            items.Add(DevExpress.Web.Internal.CommonUtils.SplitPascalCaseString(value.ToString()), value);
    }
}

public class DemoImageLoader {
    const string
        DefaultEmployeeImageName = "DefaultEmployee.png",
        ThumbnailsFolderName = "Thumb",
        ImageStringFormat = "{0}.jpg";
    object loadImagesFromDBLockObject = new object();
    Dictionary<long, byte[]> ResizedPictureHash = new Dictionary<long,byte[]>();

    string TempFolderName { get { return "TempImages_" + DataProvider.DatabaseVersion; } }

    string TempFolderVirtPath { get { return Path.Combine("~", "Content", TempFolderName); } }
    string ProductsVirtPath { get { return Path.Combine(TempFolderVirtPath, "Products"); } }
    string EmployeesVirtPath { get { return Path.Combine(TempFolderVirtPath, "Employees"); } }
    string CustomersVirtPath { get { return Path.Combine(TempFolderVirtPath, "Customers"); } }
    string CustomerEmployeesVirtPath { get { return Path.Combine(CustomersVirtPath, "CustomerEmployees"); } }
    string CrestsVirtPath { get { return Path.Combine(CustomersVirtPath, "Crests"); } }

    public string ProductImagesVirtPath(long productID) { return Path.Combine(ProductsVirtPath, productID.ToString()); }
    public string ProductThumbVirtPath(long productID) { return Path.Combine(ProductImagesVirtPath(productID), ThumbnailsFolderName); }

    public string ProductMainImageVirtPath(long productID) { return Path.Combine(ProductsVirtPath, ImageName(productID)); }
    public string ProductImageVirtPath(long productID, long imageID) { return Path.Combine(ProductImagesVirtPath(productID), ImageName(imageID)); }
    public string ProductThumbImageVirtPath(long productID, long imageID) { return Path.Combine(ProductThumbVirtPath(productID), ImageName(imageID)); }

    public string EmployeeImageVirtPath(long id) { return Path.Combine(EmployeesVirtPath, ImageName(id)); }
    public string CustomerEmployeeImageVirtPath(long id) { return Path.Combine(CustomerEmployeesVirtPath, ImageName(id)); }
    public string CrestImageVirtPath(long id) { return Path.Combine(CrestsVirtPath, ImageName(id)); }

    string TempFolderPath { get { return MapPath(TempFolderVirtPath); } }
    string ProductsPath { get { return MapPath(ProductsVirtPath); } }
    string EmployeesPath { get { return MapPath(EmployeesVirtPath); } }
    string CustomersPath { get { return MapPath(CustomersVirtPath); } }
    string CustomerEmployeesPath { get { return MapPath(CustomerEmployeesVirtPath); } }
    string CrestsPath { get { return MapPath(CrestsVirtPath); } }

    string ProductImagesPath(long productID) { return MapPath(ProductImagesVirtPath(productID)); }
    string ProductThumbPath(long productID) { return MapPath(ProductThumbVirtPath(productID)); }

    string ProductMainImagePath(long productID) { return MapPath(ProductMainImageVirtPath(productID)); }
    string ProductImagePath(long productID, long imageID) { return MapPath(ProductImageVirtPath(productID, imageID)); }
    string ProductThumbImagePath(long productID, long imageID) { return MapPath(ProductThumbImageVirtPath(productID, imageID)); }

    string EmployeeImagePath(long id) { return MapPath(EmployeeImageVirtPath(id)); }
    string CustomerEmployeeImagePath(long id) { return MapPath(CustomerEmployeeImageVirtPath(id)); }
    string CrestImagePath(long id) { return MapPath(CrestImageVirtPath(id)); }

    HttpContext Context { get { return HttpContext.Current; } }
    string DefaultEmployeeImagePath { get { return MapPath(Path.Combine("~", "Content", "Images", DefaultEmployeeImageName)); } }

    string ImageName(long id) { return string.Format(ImageStringFormat, id); }
    string MapPath(string virtualPath) { return Context.Server.MapPath(virtualPath); }

    public void EnsureImages() {
        lock(loadImagesFromDBLockObject) {
            if(!Directory.Exists(TempFolderPath))
                LoadImagesFromDB();
        }
    }
    void LoadImagesFromDB() {
        try { 
            Directory.CreateDirectory(TempFolderPath);
            LoadProductImages();
            LoadEmployeeImages();
            LoadCustomerImages();
            ResizedPictureHash.Clear();
        } catch {
            if(Directory.Exists(TempFolderPath))
                Directory.Delete(TempFolderPath, true);
        }
    }
    void LoadProductImages() {
        var productsInfo = DataProvider.Products.Select(p => new {
            ID = p.Id,
            MainPicture = p.PrimaryImage,
            Images = p.Images.Select(i => new { ID = i.Id, Picture = i.Picture }).ToList()
        }).ToList();
        Directory.CreateDirectory(ProductsPath);
        foreach(var productInfo in productsInfo) {
            Directory.CreateDirectory(ProductImagesPath(productInfo.ID));
            Directory.CreateDirectory(ProductThumbPath(productInfo.ID));
            foreach(var imageInfo in productInfo.Images) {
                CreateImage(ProductImagePath(productInfo.ID, imageInfo.ID), imageInfo.Picture.Data);
                CreateImage(ProductThumbImagePath(productInfo.ID, imageInfo.ID), imageInfo.Picture, 250, true);
            }
            CreateImage(ProductMainImagePath(productInfo.ID), productInfo.MainPicture, 250, true);
        }
    }
    void LoadEmployeeImages() {
        var employeesInfo = DataProvider.Employees.Select(e => new { ID = e.Id, Image = e.Picture }).ToList();
        Directory.CreateDirectory(EmployeesPath);
        foreach(var info in employeesInfo) {
            if(info.Image != null)
                CreateImage(EmployeeImagePath(info.ID), info.Image, 200, false);
            else
                CreateDefaultEmployeeImage(info.ID);
        }
    }
    void LoadCustomerImages() {
        var customerEmployeesInfo = DataProvider.CustomerEmployees.Select(e => new { ID = e.Id, Image = e.Picture }).ToList();
        var crestsInfo = DataProvider.Crests.Select(c => new { ID = c.Id, ImageData = c.LargeImage }).ToList();
        Directory.CreateDirectory(CustomersPath);
        Directory.CreateDirectory(CrestsPath);
        Directory.CreateDirectory(CustomerEmployeesPath);
        foreach(var info in customerEmployeesInfo)
            CreateImage(CustomerEmployeeImagePath(info.ID), info.Image, 200, false);
        foreach(var info in crestsInfo)
            CreateImage(CrestImagePath(info.ID), info.ImageData);
    }

    public void CreateDefaultEmployeeImage(long employeeID) {
        var path = EmployeeImagePath(employeeID);
        if(!File.Exists(path))
            File.Copy(DefaultEmployeeImagePath, path);
    }
    
    public void AddProductImage(Product product, byte[] data) {
        if(product == null) return;
        var image = DataProvider.CreateProductImage(product, data);
        CreateImage(ProductImagePath(product.Id, image.Id), image.Picture.Data);
        CreateImage(ProductThumbImagePath(product.Id, image.Id), image.Picture, 250, true);
    }

    byte[] ResizeImage(Picture picture, int dimension, bool isWidth) {
        if(!ResizedPictureHash.ContainsKey(picture.Id))
            ResizedPictureHash[picture.Id] = ResizeImageCore(picture.Data, dimension, isWidth);
        return ResizedPictureHash[picture.Id];
    }
    byte[] ResizeImageCore(byte[] data, int dimension, bool isWidth) {
        using(var original = Image.FromStream(new MemoryStream(data)))
        using(var newImage = InscribeImage(original, dimension, true))
        using(var outStream = new MemoryStream()) {
            newImage.Save(outStream, ImageFormat.Jpeg);
            return outStream.ToArray();
        }
    }
    void CreateImage(string path, Picture picture, int dimension, bool isWidth) {
        var data = ResizeImage(picture, dimension, isWidth);
        CreateImage(path, data);
    }
    static void CreateImage(string path, byte[] data) {
        File.WriteAllBytes(path, data);
    }

    static Image InscribeImage(Image image, int dimension, bool isWidth) {
        Size size = new Size(isWidth ? dimension : (int)image.Width * dimension / image.Height,
                            !isWidth ? dimension : (int)image.Height * dimension / image.Width);
        var result = new Bitmap(size.Width, size.Height);
        using(var graphics = Graphics.FromImage(result)) {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.DrawImage(image, new Rectangle(new Point(0, 0), size));
        }
        return result;
    }

    public string GetPriorityImageUrl(EmployeeTaskPriority priority) {
        return Path.Combine("~/Content/Images/Priority/", GetPriorityImageName(priority));
    }
    string GetPriorityImageName(EmployeeTaskPriority priority) {
        switch(priority) {
            case EmployeeTaskPriority.Low:
                return "Priority1.png";
            case EmployeeTaskPriority.Normal:
                return "Priority2.png";
            case EmployeeTaskPriority.High:
                return "Priority3.png";
            case EmployeeTaskPriority.Urgent:
                return "Priority4.png";
        }
        return string.Empty;
    }
}

public class FilterBag {
    const string
        SearchTextHiddenFieldKey = "SearchText",
        FilterControlExpressionHiddenFieldKey = "FilterControlExpression",
        CustomFilterItemsSessionKey = "5485495F-0268-48DB-A531-7D86F7A97905";

    public FilterBag(string name, Dictionary<string, FilterItem> defaultFilters, params string[] searchFieldNames) {
        Name = name;
        DefaultFilterItems = defaultFilters;
        SearchFieldNames = new HashSet<string>(searchFieldNames);
    }

    protected HttpSessionState Session { get { return DemoUtils.Context.Session; } }
    public string Name { get; private set; }

    // not modified
    protected Dictionary<string, FilterItem> DefaultFilterItems { get; private set; }
    public HashSet<string> SearchFieldNames { get; private set; }

    // depend on client state
    public string SearchText {
        get {
            string value;
            DemoUtils.TryGetClientStateValue<string>(SearchTextHiddenFieldKey, out value);
            return !string.IsNullOrEmpty(value) ? value : string.Empty;
        }
    }
    public string FilterControlExpression {
        get {
            string value;
            DemoUtils.TryGetClientStateValue<string>(FilterControlExpressionHiddenFieldKey, out value);
            return !string.IsNullOrEmpty(value) ? value : string.Empty;
        }
        set { DemoUtils.TrySetClientStateValue<string>(FilterControlExpressionHiddenFieldKey, value); }
    }
    protected Dictionary<string, FilterItem> CustomFilterItems {
        get {
            var key = CustomFilterItemsSessionKey + Name;
            if(Session[key] == null)
                Session[key] = new Dictionary<string, FilterItem>();
            return (Dictionary<string, FilterItem>)Session[key];
        }
    }

    protected CriteriaOperator SearchCriteria {
        get {
            if(string.IsNullOrEmpty(SearchText) || SearchFieldNames.Count == 0)
                return null;
            var operators = SearchFieldNames.Select(f => new FunctionOperator(FunctionOperatorType.Contains, new OperandProperty(f), SearchText)).OfType<CriteriaOperator>().ToList();
            return operators.Count > 1 ? new GroupOperator(GroupOperatorType.Or, operators) : operators[0];
        }
    }
    public string GetExpression(bool useSearch) {
        var search = useSearch ? SearchCriteria : null;
        var filterControl = CriteriaOperator.Parse(FilterControlExpression);
        var criteria = GroupOperator.And(filterControl, search);
        return !object.ReferenceEquals(criteria, null) ? criteria.ToString() : string.Empty;
    }

    public Dictionary<string, FilterItem> GetFilterItems() {
        return DefaultFilterItems.Union(CustomFilterItems).ToDictionary(p => p.Key, p => p.Value);
    }
    public FilterItem CreateCustomFilter(string name, string criteria) {
        return CustomFilterItems.CreateItem(name, criteria, true);
    }

    public string GetActiveFilterName() {
        var item = GetFilterItems().FirstOrDefault(p => GetIsActiveFilter(p.Value.Expression));
        return !object.ReferenceEquals(item, null) ? item.Key : string.Empty;
    }
    public bool GetIsActiveFilter(string expression) {
        return expression == FilterControlExpression;
    }

    public static CriteriaOperator GetSearchCriteria(string searchText, params string[] fieldNames) {
        if(string.IsNullOrEmpty(searchText) || fieldNames.Length == 0)
            return null;
        var operators = fieldNames.Select(f => new FunctionOperator(FunctionOperatorType.Contains, new OperandProperty(f), searchText)).OfType<CriteriaOperator>().ToList();
        return operators.Count > 1 ? new GroupOperator(GroupOperatorType.Or, operators) : operators[0];
    }
}

public static class DemoExtentionUtils {
    public static FilterItem CreateItem(this Dictionary<string, FilterItem> self, string name, string criteria, bool custom = false) {
        return self.CreateItem(name, CriteriaOperator.Parse(criteria), custom);
    }
    public static FilterItem CreateItem(this Dictionary<string, FilterItem> self, string name, CriteriaOperator criteria, bool custom = false) {
        var key = string.Format("{0}_{1}", name, custom);
        if(!self.ContainsKey(key) || custom) {
            var expression = !object.ReferenceEquals(criteria, null) ? criteria.ToString() : string.Empty;
            self[key] = new FilterItem() { Name = name, Expression = expression, IsCustom = custom };
        }
        return self[key];
    }
    public static T Add<T>(this ArrayList self, T item) {
        self.Add(item);
        return item;
    }
    public static FilterControlComboBoxColumn Add(this ArrayList self, FilterControlComboBoxColumn column, Type enumType) {
        self.Add(column);
        DemoUtils.BindComboBoxToEnum(column.PropertiesComboBox, enumType);
        return column;
    }

    public static TableRow Add(this TableRowCollection collection) { // TODO check why it doesn't work
        var row = new TableRow();
        collection.Add(row);
        return row;
    }
}