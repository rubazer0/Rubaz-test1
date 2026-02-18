using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using MainCore.Entities;
using System; // Necessário para Exception

namespace MainCore.Helpers
{
    public static class TelegramHelper
    {
        private static readonly HttpClient client = new HttpClient();

        public class TelegramSettings
        {
            public string BotToken { get; set; } = "";
            public string ChatId { get; set; } = "";
        }

        private static string GetFolder()
        {
            var path = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "TelegramSettings");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            return path;
        }

        private static string GetFilePath(AccountId accountId)
        {
            return Path.Combine(GetFolder(), $"settings_{accountId.Value}.json");
        }

        public static void SaveSettings(AccountId accountId, string token, string chatId)
        {
            var settings = new TelegramSettings { BotToken = token, ChatId = chatId };
            var json = JsonSerializer.Serialize(settings);
            File.WriteAllText(GetFilePath(accountId), json);
        }

        public static TelegramSettings GetSettings(AccountId accountId)
        {
            var path = GetFilePath(accountId);
            if (!File.Exists(path)) return new TelegramSettings();

            try
            {
                var json = File.ReadAllText(path);
                var settings = JsonSerializer.Deserialize<TelegramSettings>(json);
                return settings ?? new TelegramSettings();
            }
            catch
            {
                return new TelegramSettings();
            }
        }

        public static async Task SendMessage(AccountId accountId, string message)
        {
            var settings = GetSettings(accountId);
            if (string.IsNullOrEmpty(settings.BotToken) || string.IsNullOrEmpty(settings.ChatId)) return;

            try
            {
                string url = $"https://api.telegram.org/bot{settings.BotToken}/sendMessage?chat_id={settings.ChatId}&text={message}";
                await client.GetAsync(url);
            }
            catch
            {
                // Falha silenciosa para não travar o bot durante o farm
            }
        }

        // --- NOVO MÉTODO DE TESTE (Retorna erro se falhar) ---
        public static async Task TestSettings(string token, string chatId)
        {
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(chatId))
                throw new Exception("Token ou Chat ID estão vazios.");

            string message = "🔔 Teste de Notificação do Rubaz Bot! Se você leu isso, está configurado corretamente.";
            string url = $"https://api.telegram.org/bot{token}/sendMessage?chat_id={chatId}&text={message}";

            var response = await client.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Erro do Telegram: {response.StatusCode} - {response.ReasonPhrase}");
            }
        }
    }
}
