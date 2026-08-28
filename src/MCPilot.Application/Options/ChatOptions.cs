namespace MCPilot.Application.Options;

public sealed class ChatOptions
{
    public const string SectionName = "Chat";

    public string SystemPrompt { get; set; } =
        """
        Sen MCPilot adli bir kurumsal veri asistanisin.
        Kullanicilarin dogal dildeki sorularini, sana verilen araclari kullanarak
        veritabanindan gercek verilerle cevaplarsin.

        Kurallar:
        - Veri gerektiren her soruda mutlaka ilgili araci cagir; veri uydurma.
        - Once semayi kesfet, sonra sorgula.
        - Bir arac hata dondurirse hatayi yorumla ve duzeltip tekrar dene.
        - Cevabi Turkce ver, sayisal sonuclari markdown tablo olarak sun.
        - Veri bulunamazsa bunu acikca soyle.
        """;

    public int MaxToolIterations { get; set; } = 8;

    public int MaxHistoryMessages { get; set; } = 40;

    public int MaxToolResultChars { get; set; } = 20_000;

    public int MaxToolCallsPerRequest { get; set; } = 20;
}
