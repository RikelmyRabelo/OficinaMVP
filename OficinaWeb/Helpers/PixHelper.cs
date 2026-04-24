using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace OficinaWeb.Helpers
{
    public static class PixHelper
    {
        private const string IdPayloadFormatIndicator = "00";
        private const string IdMerchantAccountInformation = "26";
        private const string IdMerchantCategoryCode = "52";
        private const string IdTransactionCurrency = "53";
        private const string IdTransactionAmount = "54";
        private const string IdCountryCode = "58";
        private const string IdMerchantName = "59";
        private const string IdMerchantCity = "60";
        private const string IdAdditionalDataFieldTemplate = "62";
        private const string IdCrc16 = "63";

        public static string GerarPayloadPix(string chave, string nome, string cidade, decimal valor, string txid = "***")
        {
            string GetValue(string id, string val) => $"{id}{val.Length:D2}{val}";

            nome = SanitizarInput(nome, 25);
            cidade = SanitizarInput(cidade, 15);

            string merchantAccountInfo = GetValue("00", "br.gov.bcb.pix") + GetValue("01", chave);

            string payload = GetValue(IdPayloadFormatIndicator, "01") +
                             GetValue(IdMerchantAccountInformation, merchantAccountInfo) +
                             GetValue(IdMerchantCategoryCode, "0000") +
                             GetValue(IdTransactionCurrency, "986") +
                             GetValue(IdTransactionAmount, valor.ToString("F2", CultureInfo.InvariantCulture)) +
                             GetValue(IdCountryCode, "BR") +
                             GetValue(IdMerchantName, nome) +
                             GetValue(IdMerchantCity, cidade) +
                             GetValue(IdAdditionalDataFieldTemplate, GetValue("05", txid));

            string finalPart = IdCrc16 + "04";
            string payloadComCrcPlaceholder = payload + finalPart;
            return payloadComCrcPlaceholder + CalcularCRC16(payloadComCrcPlaceholder);
        }

        private static string SanitizarInput(string input, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(input)) return "OFICINA";

            string normalizado = input.Normalize(NormalizationForm.FormD);
            StringBuilder sb = new StringBuilder();

            foreach (char c in normalizado)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }

            string limpo = Regex.Replace(sb.ToString().Normalize(NormalizationForm.FormC), @"[^a-zA-Z0-9 ]", "");
            string resultado = limpo.Length > maxLength ? limpo.Substring(0, maxLength) : limpo;

            return resultado.Trim().ToUpper();
        }

        private static string CalcularCRC16(string payload)
        {
            ushort polinomio = 0x1021;
            ushort res = 0xFFFF;
            byte[] data = Encoding.ASCII.GetBytes(payload);

            foreach (byte b in data)
            {
                res ^= (ushort)(b << 8);
                for (int i = 0; i < 8; i++)
                {
                    if ((res & 0x8000) != 0)
                        res = (ushort)((res << 1) ^ polinomio);
                    else
                        res <<= 1;
                }
            }
            return res.ToString("X4");
        }
    }
}