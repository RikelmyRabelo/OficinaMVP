using System;
using System.Globalization;
using System.Text;

namespace OficinaWeb.Helpers
{
    public static class PixHelper
    {
        public static string GerarPayloadPix(string chave, string nome, string cidade, decimal valor)
        {
            string GetValue(string id, string val) => $"{id}{val.Length.ToString("D2")}{val}";
            nome = nome.Length > 25 ? nome.Substring(0, 25) : nome;
            cidade = cidade.Length > 15 ? cidade.Substring(0, 15) : cidade;

            string payload = GetValue("00", "01") +
                             GetValue("26", GetValue("00", "br.gov.bcb.pix") + GetValue("01", chave)) +
                             GetValue("52", "0000") +
                             GetValue("53", "986") +
                             GetValue("54", valor.ToString("F2", CultureInfo.InvariantCulture)) +
                             GetValue("58", "BR") +
                             GetValue("59", nome) +
                             GetValue("60", cidade) +
                             GetValue("62", GetValue("05", "***"));

            return payload + "6304" + CalcularCRC16(payload + "6304");
        }

        private static string CalcularCRC16(string payload)
        {
            ushort polinomio = 0x1021;
            ushort res = 0xFFFF;
            byte[] data = Encoding.UTF8.GetBytes(payload);

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