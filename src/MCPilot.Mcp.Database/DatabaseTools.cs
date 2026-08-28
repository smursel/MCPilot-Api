using System.ComponentModel;
using ModelContextProtocol.Server;

namespace MCPilot.Mcp.Database;

[McpServerToolType]
public static class DatabaseTools
{
    private static string? ConnectionString => Environment.GetEnvironmentVariable("MCPILOT_DB_CONNECTION");

    [McpServerTool(Name = "list_tables", ReadOnly = true)]
    [Description("Veritabanindaki tablolari ve kisa aciklamalarini listeler. Sorgu yazmadan once bunu cagir.")]
    public static object ListTables() => new
    {
        source = "ORNEK VERI",
        tables = new object[]
        {
            new { name = "Musteriler", description = "Musteri ana kayitlari" },
            new { name = "Siparisler", description = "Siparis basliklari (tarih, musteri, tutar)" },
            new { name = "SiparisDetay", description = "Siparis satirlari (urun, adet, birim fiyat)" },
            new { name = "Urunler", description = "Urun katalogu" },
        },
    };

    [McpServerTool(Name = "describe_table", ReadOnly = true)]
    [Description("Verilen tablonun kolonlarini ve tiplerini dondurur.")]
    public static object DescribeTable([Description("Tablo adi, ornegin 'Siparisler'")] string table) => new
    {
        source = "ORNEK VERI",
        table,
        columns = new object[]
        {
            new { name = "Id", type = "int", nullable = false },
            new { name = "MusteriId", type = "int", nullable = false },
            new { name = "Tarih", type = "datetime", nullable = false },
            new { name = "Tutar", type = "decimal(18,2)", nullable = false },
        },
    };

    [McpServerTool(Name = "run_query", ReadOnly = true)]
    [Description("Salt-okunur SQL sorgusu calistirir ve satirlari dondurur. Sadece SELECT kullan, limit ver.")]
    public static object RunQuery(
        [Description("Calistirilacak SELECT sorgusu")] string sql,
        [Description("Dondurulecek maksimum satir sayisi")] int limit = 100)
    {
        if (!sql.TrimStart().StartsWith("select", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Sadece SELECT sorgularina izin verilir.");
        }

        return new
        {
            source = "ORNEK VERI",
            note = ConnectionString is null
                ? "MCPILOT_DB_CONNECTION tanimli degil; ornek veri donuluyor."
                : "Gercek sorgu henuz baglanmadi; ornek veri donuluyor.",
            sql,
            limit,
            rowCount = 2,
            rows = new object[]
            {
                new { Urun = "Klavye", Adet = 128, Tutar = 96_000m },
                new { Urun = "Monitor", Adet = 74, Tutar = 355_200m },
            },
        };
    }
}
