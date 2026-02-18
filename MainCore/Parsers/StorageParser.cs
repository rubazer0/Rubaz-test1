using HtmlAgilityPack;
using System.Linq;

namespace MainCore.Parsers
{
    public static class StorageParser
    {
        // Função auxiliar para limpar números do Travian (remove virgulas e caracteres invisiveis)
        private static long ParseTravianNumber(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            var cleanText = new string(text.Where(char.IsDigit).ToArray());
            if (long.TryParse(cleanText, out long result)) return result;
            return 0;
        }

        public static long GetCrop(HtmlDocument doc)
        {
            // Busca o elemento do cereal (id="l4")
            var node = doc.GetElementbyId("l4");
            if (node == null) return -1;
            return ParseTravianNumber(node.InnerText);
        }

        public static long GetGranaryCapacity(HtmlDocument doc)
        {
            // 1. Procura a div principal "granary" em qualquer lugar do documento
            var granaryNode = doc.DocumentNode.Descendants("div")
                .FirstOrDefault(x => x.HasClass("granary"));

            if (granaryNode == null) return -1;

            // 2. Procura a div "capacity" dentro dela
            var capacityNode = granaryNode.Descendants("div")
                .FirstOrDefault(x => x.HasClass("capacity"));

            if (capacityNode == null) return -1;

            // 3. Procura a div "value" dentro de capacity
            var valueNode = capacityNode.Descendants("div")
                .FirstOrDefault(x => x.HasClass("value"));

            if (valueNode == null) return -1;

            return ParseTravianNumber(valueNode.InnerText);
        }

        // Mantemos os outros métodos para compatibilidade, mas usando nossa limpeza de numero
        public static long GetWood(HtmlDocument doc) => GetResource(doc, "l1");
        public static long GetClay(HtmlDocument doc) => GetResource(doc, "l2");
        public static long GetIron(HtmlDocument doc) => GetResource(doc, "l3");
        public static long GetFreeCrop(HtmlDocument doc) => GetResource(doc, "stockBarFreeCrop");

        private static long GetResource(HtmlDocument doc, string id)
        {
            var node = doc.GetElementbyId(id);
            if (node is null) return -1;
            return ParseTravianNumber(node.InnerText);
        }

        public static long GetWarehouseCapacity(HtmlDocument doc)
        {
            // Mesma lógica robusta para o Armazém
            var warehouseNode = doc.DocumentNode.Descendants("div")
                .FirstOrDefault(x => x.HasClass("warehouse"));

            if (warehouseNode == null) return -1;

            var capacityNode = warehouseNode.Descendants("div")
                .FirstOrDefault(x => x.HasClass("capacity"));
            if (capacityNode == null) return -1;

            var valueNode = capacityNode.Descendants("div")
                .FirstOrDefault(x => x.HasClass("value"));
            if (valueNode == null) return -1;

            return ParseTravianNumber(valueNode.InnerText);
        }
    }
}
