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

        if (string.IsNullOrWhiteSpace(connStr))
        {
            throw new InvalidOperationException(
                "MCPILOT_DB_CONNECTION tanimli degil. API'nin Mcp:Servers[].Environment ayarindan gelir.");
        }

        return NormalizeConnectionString(connStr);
    }

    // Neon "postgresql://kullanici:sifre@host/db?sslmode=require" bicimini uretir,
    // Npgsql ise "Host=...;Username=..." anahtar/deger bicimini bekler.
    // Zaten anahtar/deger bicimindeyse oldugu gibi birakilir.
    private static string NormalizeConnectionString(string value)
    {
        if (!value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
            !value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        var uri = new Uri(value);
        var userInfo = uri.UserInfo.Split(':', 2);

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = uri.AbsolutePath.TrimStart('/'),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null,
            SslMode = SslMode.Require,
            TrustServerCertificate = true,
        };

        return builder.ConnectionString;
    }

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
            
        return connection.QueryFirstOrDefault(sqlQuery, new { StartDate = from, EndDate = to });
    }

    [McpServerTool(Name = "get_top_products", ReadOnly = true)] //[cite: 18]
    [Description("Belirli bir tarih aralığındaki en çok satan ürünleri (adet ve ciro bazında) döndürür.")] //[cite: 18]
    public static object GetTopProducts(
        [Description("Başlangıç tarihi (yyyy-MM-dd)")] string from, //[cite: 18]
        [Description("Bitiş tarihi (yyyy-MM-dd)")] string to, //[cite: 18]
        [Description("Kaç ürün döndürülecek (varsayılan 5)")] int limit = 5) //[cite: 18]
    {
        using var connection = new NpgsqlConnection(GetConnectionString()); //[cite: 18]
        var sqlQuery = @"
            SELECT ProductName, CategoryName, SUM(Quantity) AS TotalUnits, SUM(LineRevenue) AS TotalRevenue
            FROM vw_SalesAnalytics
            WHERE OrderDate >= @StartDate::timestamp AND OrderDate <= @EndDate::timestamp
            GROUP BY ProductName, CategoryName
            ORDER BY TotalUnits DESC
            LIMIT @Limit;";
            
        return connection.Query(sqlQuery, new { StartDate = from, EndDate = to, Limit = limit }); //[cite: 18]
    }

    [McpServerTool(Name = "get_category_sales", ReadOnly = true)]
    [Description("Hangi ürün kategorisinin (örn. Elektronik, Kozmetik) ne kadar gelir getirdiğini ve adet sattığını döndürür.")]
    public static object GetCategorySales(
        [Description("Başlangıç tarihi (yyyy-MM-dd)")] string from,
        [Description("Bitiş tarihi (yyyy-MM-dd)")] string to)
    {
        using var connection = new NpgsqlConnection(GetConnectionString());
        var sqlQuery = @"
            SELECT CategoryName, SUM(LineRevenue) AS TotalRevenue, SUM(Quantity) AS TotalQuantity
            FROM vw_SalesAnalytics
            WHERE OrderDate >= @StartDate::timestamp AND OrderDate <= @EndDate::timestamp
            GROUP BY CategoryName
            ORDER BY TotalRevenue DESC;";
            
        return connection.Query(sqlQuery, new { StartDate = from, EndDate = to });
    }

    [McpServerTool(Name = "get_city_sales", ReadOnly = true)] //[cite: 18]
    [Description("Belirli bir tarih aralığında şehirlere göre satış (ciro) ve kar dağılımını döndürür. Bölgesel analizler için kullanılır.")] //[cite: 18]
    public static object GetCitySales(
        [Description("Başlangıç tarihi (yyyy-MM-dd)")] string from, //[cite: 18]
        [Description("Bitiş tarihi (yyyy-MM-dd)")] string to) //[cite: 18]
    {
        using var connection = new NpgsqlConnection(GetConnectionString()); //[cite: 18]
        var sqlQuery = @"
            SELECT City, SUM(LineRevenue) AS TotalRevenue, SUM(LineProfit) AS TotalProfit
            FROM vw_SalesAnalytics
            WHERE OrderDate >= @StartDate::timestamp AND OrderDate <= @EndDate::timestamp
            GROUP BY City
            ORDER BY TotalRevenue DESC;";
            
        return connection.Query(sqlQuery, new { StartDate = from, EndDate = to }); //[cite: 18]
    }

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

    [McpServerTool(Name = "get_top_customers", ReadOnly = true)] //[cite: 18]
    [Description("Şirkete en çok gelir sağlayan, sadakat puanı ve toplam değeri (LTV) en yüksek olan VIP müşterileri listeler.")] //[cite: 18]
    public static object GetTopCustomers(
        [Description("Döndürülecek müşteri sayısı (varsayılan 5)")] int limit = 5) //[cite: 18]
    {
        using var connection = new NpgsqlConnection(GetConnectionString()); //[cite: 18]
        var sqlQuery = @"
            SELECT CustomerFullName, CurrentSegment, LoyaltyScore, LifeTimeValue, TotalOrders
            FROM vw_CustomerInsights
            ORDER BY LoyaltyScore DESC
            LIMIT @Limit;";
            
        return connection.Query(sqlQuery, new { Limit = limit }); //[cite: 18]
    }

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

    [McpServerTool(Name = "get_monthly_trend", ReadOnly = true)] //[cite: 18]
    [Description("Belirli bir yılın aylık bazda toplam ciro ve kar trendini döndürür. Mevsimsel yükseliş ve düşüşleri görmek için kullanılır.")] //[cite: 18]
    public static object GetMonthlyTrend(
        [Description("Analiz edilecek yıl (örneğin: 2026)")] int year) //[cite: 18]
    {
        using var connection = new NpgsqlConnection(GetConnectionString()); //[cite: 18]
        var sqlQuery = @"
            SELECT Month, TotalRevenue, TotalProfit, TotalOrders
            FROM MonthlyFinancials
            WHERE Year = @TargetYear
            ORDER BY Month ASC;";
            
        return connection.Query(sqlQuery, new { TargetYear = year }); //[cite: 18]
    }
}
