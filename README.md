# MCPilot API

Angular arayuzunden gelen dogal dil sorularini, Claude (LLM) + MCP araclari uzerinden
veritabanina baglayip veri/rapor olarak geri donen .NET 8 Web API iskeleti.

```
Kullanici → Angular → .NET API → AI/LLM (Claude) → MCP → Veritabani
                                        ↑                     │
                                        └──── veri / rapor ────┘
                                        ↓
                            Kullanici (cevap + tablo + iz kaydi)
```

Bu depo **API + LLM + MCP** taraflarini icerir. **Veritabani fonksiyonlari** ayri bir MCP
sunucusunda yasar (`src/MCPilot.Mcp.Database`) ve bagimsiz gelistirilebilir; API'de kod
degisikligi gerektirmez.

## Proje yapisi

| Proje | Sorumluluk |
|---|---|
| `src/MCPilot.Api` | HTTP ucu: controller'lar, CORS, SSE, Swagger, hata yonetimi |
| `src/MCPilot.Application` | Saglayicidan bagimsiz sozlesmeler + sohbet/arac dongusu (`ChatOrchestrator`) |
| `src/MCPilot.Infrastructure` | Claude istemcisi, MCP istemcisi/katalogu, konusma deposu, DI |
| `src/MCPilot.Mcp.Database` | **Ornek MCP sunucusu — veritabani ekibinin gelistirecegi yer** |

Bagimlilik yonu: `Api → Infrastructure → Application`. Application katmani hicbir SDK'ya
bagimli degildir; LLM saglayicisi degistirilecekse yalnizca `ILlmClient` yeniden yazilir.

## Akis (ChatOrchestrator)

1. Kullanici mesaji konusma gecmisine eklenir (`IConversationStore`).
2. MCP sunucularindaki araclar kesfedilir (`IToolCatalog`) ve `ToolPolicy` ile filtrelenir.
3. Claude'a mesajlar + arac tanimlari gonderilir.
4. Model `tool_use` dondururse ilgili MCP araci calistirilir, sonuc `tool_result` olarak modele
   geri verilir. Bu dongu cevap uretilene veya `Chat:MaxToolIterations` limitine kadar surer.
5. Cevap, arac cagri izleri (`toolCalls`) ve token kullanimi ile birlikte doner.

Her arac cagrisinin izi (`arguments`, `result`, `structuredResult`, `durationMs`) yanitta yer
alir; Angular bunlari "veri nereden geldi" paneli veya tablo/grafik render etmek icin kullanabilir.

## Calistirma

```bash
# 1) API anahtari
export ANTHROPIC_API_KEY=sk-ant-...      # veya appsettings.json > Anthropic:ApiKey

# 2) MCP sunucusunu derle (API onu alt surec olarak baslatir)
dotnet build

# 3) API
dotnet run --project src/MCPilot.Api
```

- Swagger: http://localhost:5080/swagger
- Saglik: http://localhost:5080/api/health → baglanan MCP sunuculari ve arac sayisi

## Uc noktalar

| Metot | Yol | Aciklama |
|---|---|---|
| POST | `/api/chat` | Soru sorar, tam cevabi doner |
| POST | `/api/chat/stream` | Ayni akis, SSE (`started`, `assistant_text`, `tool_call`, `tool_result`, `completed`, `failed`) |
| GET | `/api/tools` | Kesfedilen MCP araclari + politika durumu |
| POST | `/api/tools/refresh` | MCP baglantilarini yenile (yeni arac eklendiginde) |
| POST | `/api/tools/{name}/invoke` | Bir araci LLM olmadan dogrudan calistir (test icin) |
| GET | `/api/conversations` | Konusma listesi |
| GET | `/api/conversations/{id}` | Konusma gecmisi |
| DELETE | `/api/conversations/{id}` | Konusmayi sil |
| GET | `/api/health` | Durum |

### Angular ornegi

```ts
// Tam yanit
const res = await this.http.post<ChatResponse>('/api/chat', {
  message, conversationId: this.conversationId,
}).toPromise();

// SSE (EventSource POST desteklemez; fetch + ReadableStream kullanin)
const response = await fetch('/api/chat/stream', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ message, conversationId }),
});
const reader = response.body!.pipeThrough(new TextDecoderStream()).getReader();
// "event: tool_call\ndata: {...}\n\n" bloklarini ayristirin
```

## Yapilandirma (`appsettings.json`)

```jsonc
"Anthropic": {
  "ApiKey": "",                 // bos ise ANTHROPIC_API_KEY ortam degiskeni kullanilir
  "Model": "claude-opus-5",
  "MaxTokens": 16000,
  "Effort": "high",             // low | medium | high | max
  "EnableThinking": true        // adaptive thinking
},
"Chat": {
  "MaxToolIterations": 8,       // model-arac dongusu ust siniri
  "MaxHistoryMessages": 40,     // modele gonderilen gecmis penceresi
  "MaxToolResultChars": 20000   // tool sonucu kirpma siniri
},
"ToolPolicy": {
  "Mode": "Auto",               // Auto | ReadOnly | Allowlist
  "Allowed": [ "db__*" ],
  "Denied": [],
  "MaxToolCallsPerRequest": 20
},
"Mcp": {
  "Servers": [
    {
      "Name": "db",             // arac adlari: db__run_query
      "Transport": "Stdio",     // Stdio | Http
      "Command": "dotnet",
      "Arguments": [ "../MCPilot.Mcp.Database/bin/Debug/net8.0/MCPilot.Mcp.Database.dll" ],
      "Environment": { "MCPILOT_DB_CONNECTION": "" }
    }
  ]
}
```

`ToolPolicy.Mode = "ReadOnly"` yapildiginda yalnizca MCP `readOnlyHint` ile isaretli araclar
modele sunulur — yazma yetkisi olan araclari kapatmanin en hizli yolu.

HTTP tasiyicili bir MCP sunucusu eklemek icin:

```jsonc
{ "Name": "reports", "Transport": "Http", "Endpoint": "http://localhost:3001",
  "Headers": { "Authorization": "Bearer ..." } }
```

## Veritabani ekibi icin

Tum is `src/MCPilot.Mcp.Database/DatabaseTools.cs` icinde. Bir metot ekleyip
`[McpServerTool]` + `[Description]` ile isaretlemek yeterli:

```csharp
[McpServerTool(Name = "top_products", ReadOnly = true)]
[Description("Verilen tarih araligindaki en cok satan urunleri dondurur.")]
public static object TopProducts(
    [Description("Baslangic tarihi (yyyy-MM-dd)")] string from,
    [Description("Bitis tarihi (yyyy-MM-dd)")] string to,
    [Description("Kac urun dondurulecek")] int top = 5) => /* Dapper / EF sorgusu */;
```

Notlar:
- `[Description]` metinleri modelin araci dogru kullanmasini saglar; net ve kisa yazin.
- Salt-okunur araclarda `ReadOnly = true` kullanin.
- stdio sunucusunda **stdout protokole aittir**; loglari stderr'a yazin (`Program.cs` ayarli).
- Derledikten sonra API'de `POST /api/tools/refresh` cagirin; arac aninda modele sunulur.
- Test icin LLM'e gerek yok: `POST /api/tools/db__top_products/invoke` ile dogrudan cagirin.

## Uretim icin yapilacaklar

- `IConversationStore` icin kalici implementasyon (EF Core / Redis) — DI kaydini degistirmek yeterli.
- Kimlik dogrulama/yetkilendirme (JWT) ve `ChatRequest.UserId` yerine token'dan kullanici cozumleme.
- MCP sunucusunun salt-okunur DB kullanicisiyla calismasi ve sorgu zaman asimi.
- Rate limiting (`AddRateLimiter`) ve istek basina token/maliyet limiti.
