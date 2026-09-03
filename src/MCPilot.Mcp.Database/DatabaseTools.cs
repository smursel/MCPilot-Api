using System;
using System.ComponentModel;
using Dapper;
using Npgsql;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Server;

namespace MCPilot.Mcp.Database;

[McpServerToolType]
public static class DatabaseTools
{
    // Fetches the Neon connection string passed from the Main API's environment variables.
    private static string GetConnectionString()
    {
        var connStr = Environment.GetEnvironmentVariable("MCPILOT_DB_CONNECTION");
        return connStr ?? throw new Exception("Database connection string is missing, please Check appsettings.");
    }

   //GetSalesSummary
   [McpServerTool(Name = "get_sales_summary", ReadOnly = true)]
   [Description("Belirli bir dönemdeki Toplam Ciro, Toplam Kar, Toplam Sipariş Sayısı ve Ortalama Sipariş Tutarını döndürür.")]
   public static object GetSalesSummary(
        [Description("Başlangıç tarihi (yyyy-MM-dd)")] string from,
        [Description("Bitiş tarihi (yyyy-MM-dd)")] string to)
    {
        using var connection = new NpgsqlConnection(GetConnectionString());
        var sqlQuery = @"
            SELECT 
                SUM(LineRevenue) AS TotalRevenue,
                SUM(LineProfit) AS TotalProfit,
                COUNT(DISTINCT OrderID) AS TotalOrders,
                CASE WHEN COUNT(DISTINCT OrderID) = 0 THEN 0 ELSE SUM(LineRevenue) / COUNT(DISTINCT OrderID) END AS AverageOrderValue
            FROM vw_SalesAnalytics
            WHERE OrderDate >= @StartDate::timestamp AND OrderDate <= @EndDate::timestamp;";
            
        return connection.QueryFirstOrDefault(sqlQuery, new { StartDate = from, EndDate = to })!;
    }

    //GetTopProducts
    [McpServerTool(Name = "get_top_products", ReadOnly = true)] 
    [Description("Belirli bir tarih aralığındaki en çok satan ürünleri (adet ve ciro bazında) döndürür.")]  
    public static object GetTopProducts(
        [Description("Başlangıç tarihi (yyyy-MM-dd)")] string from, 
        [Description("Bitiş tarihi (yyyy-MM-dd)")] string to, 
        [Description("Opsiyonel kategori adı filtresi (örn. Electronics)")] string? category = null,
        [Description("Kaç ürün döndürülecek (varsayılan 5)")] int limit = 5) 
    {
        using var connection = new NpgsqlConnection(GetConnectionString()); 
        var sqlQuery = @"
            SELECT ProductName, CategoryName, SUM(Quantity) AS TotalUnits, SUM(LineRevenue) AS TotalRevenue, SUM(LineProfit) AS TotalProfit
            FROM vw_SalesAnalytics
            WHERE OrderDate >= @StartDate::timestamp AND OrderDate <= @EndDate::timestamp
              AND (@CategoryName::text IS NULL OR CategoryName = @CategoryName::text)
            GROUP BY ProductName, CategoryName
            ORDER BY TotalUnits DESC
            LIMIT @Limit;";
            
        return connection.Query(sqlQuery, new { 
            StartDate = from,
            EndDate = to,
            CategoryName = string.IsNullOrWhiteSpace(category) ? null : category,
            Limit = limit });
        }

    //GetCategorySales
    [McpServerTool(Name = "get_category_sales", ReadOnly = true)]
   [Description("Ürün kategorilerinin ciro, kâr ve adet performansını döndürür.")]    
   public static object GetCategorySales(
        [Description("Başlangıç tarihi (yyyy-MM-dd)")] string from,
        [Description("Bitiş tarihi (yyyy-MM-dd)")] string to)
    {
        using var connection = new NpgsqlConnection(GetConnectionString());
        var sqlQuery = @"
            SELECT CategoryName, SUM(LineRevenue) AS TotalRevenue, SUM(LineProfit) AS TotalProfit, SUM(Quantity) AS TotalQuantity
            FROM vw_SalesAnalytics
            WHERE OrderDate >= @StartDate::timestamp AND OrderDate <= @EndDate::timestamp
            GROUP BY CategoryName
            ORDER BY TotalRevenue DESC;";
            
        return connection.Query(sqlQuery, new { StartDate = from, EndDate = to });
    }
    
    //GetCitySales
    [McpServerTool(Name = "get_city_sales", ReadOnly = true)] 
    [Description("Belirli bir tarih aralığında şehirlere göre satış (ciro) ve kar dağılımını döndürür. Bölgesel analizler için kullanılır.")] 
    public static object GetCitySales(
        [Description("Başlangıç tarihi (yyyy-MM-dd)")] string from, 
        [Description("Bitiş tarihi (yyyy-MM-dd)")] string to)
    {
        using var connection = new NpgsqlConnection(GetConnectionString());
        var sqlQuery = @"
            SELECT City, SUM(LineRevenue) AS TotalRevenue, SUM(LineProfit) AS TotalProfit
            FROM vw_SalesAnalytics
            WHERE OrderDate >= @StartDate::timestamp AND OrderDate <= @EndDate::timestamp
            GROUP BY City
            ORDER BY TotalRevenue DESC;";
            
        return connection.Query(sqlQuery, new { StartDate = from, EndDate = to });
    }

    //GetCountrySales
    [McpServerTool(Name = "get_country_sales", ReadOnly = true)]
    [Description("Şirketin uluslararası pazardaki performansını ve ülkelere göre gelir dağılımını gösterir.")]
    public static object GetCountrySales(
        [Description("Başlangıç tarihi (yyyy-MM-dd)")] string from,
        [Description("Bitiş tarihi (yyyy-MM-dd)")] string to)
    {
        using var connection = new NpgsqlConnection(GetConnectionString());
        var sqlQuery = @"
            SELECT Country, SUM(LineRevenue) AS TotalRevenue, SUM(LineProfit) AS TotalProfit
            FROM vw_SalesAnalytics
            WHERE OrderDate >= @StartDate::timestamp AND OrderDate <= @EndDate::timestamp
            GROUP BY Country
            ORDER BY TotalRevenue DESC;";
            
        return connection.Query(sqlQuery, new { StartDate = from, EndDate = to });
    }

    //GetProductProfitMargins
    [McpServerTool(Name = "get_product_profit_margins", ReadOnly = true)]
    [Description("Ürünlerin güncel satış fiyatı, maliyeti ve hesaplanan kar marjı yüzdelerini listeler.")]
    public static object GetProductProfitMargins(
        [Description("Döndürülecek ürün sayısı (varsayılan 10)")] int limit = 10)
    {
        using var connection = new NpgsqlConnection(GetConnectionString());
        var sqlQuery = @"
            SELECT name AS ProductName, ProfitMargin
            FROM vw_CurrentProductProfitMargin
            ORDER BY ProfitMargin DESC
            LIMIT @Limit;";
            
        return connection.Query(sqlQuery, new { Limit = limit });
    }

    //GetTopCustomers
    [McpServerTool(Name = "get_top_customers", ReadOnly = true)] 
    [Description("Şirkete en çok gelir sağlayan, sadakat puanı ve toplam değeri (LTV) en yüksek olan VIP müşterileri listeler.")] 
    public static object GetTopCustomers(
        [Description("Döndürülecek müşteri sayısı (varsayılan 5)")] int limit = 5)
    {
        using var connection = new NpgsqlConnection(GetConnectionString());
        var sqlQuery = @"
            SELECT CustomerFullName, CurrentSegment, LoyaltyScore, LifeTimeValue, TotalOrders
            FROM vw_CustomerInsights
            ORDER BY LoyaltyScore DESC
            LIMIT @Limit;";
            
        return connection.Query(sqlQuery, new { Limit = limit });
    }

    //GetCustomerSegments
    [McpServerTool(Name = "get_customer_segments", ReadOnly = true)]
    [Description("Müşterilerin harcama alışkanlıklarına göre ayrıldığı grupların (VIP, Loyal vb.) toplam ciro içindeki payını gösterir.")]
    public static object GetCustomerSegments()
    {
        using var connection = new NpgsqlConnection(GetConnectionString());
        var sqlQuery = @"
            SELECT CurrentSegment, SUM(LifeTimeValue) AS TotalRevenue, COUNT(CustomerID) AS CustomerCount
            FROM vw_CustomerInsights
            GROUP BY CurrentSegment
            ORDER BY TotalRevenue DESC;";
            
        return connection.Query(sqlQuery);
    }

    //GetMonthlyTrend
    [McpServerTool(Name = "get_monthly_trend", ReadOnly = true)]
    [Description("Belirli bir yılın aylık bazda toplam ciro ve kar trendini döndürür. Mevsimsel yükseliş ve düşüşleri görmek için kullanılır.")] 
    public static object GetMonthlyTrend(
        [Description("Analiz edilecek yıl (örneğin: 2026)")] int year) 
    {
        using var connection = new NpgsqlConnection(GetConnectionString());
        var sqlQuery = @"
            SELECT Month, TotalRevenue, TotalProfit, TotalOrders
            FROM MonthlyFinancials
            WHERE Year = @TargetYear
            ORDER BY Month ASC;";
            
        return connection.Query(sqlQuery, new { TargetYear = year });
    }
}
