using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace RouteXWms.Services
{
    /// <summary>
    /// CSVデータの読み込み・書き出しおよび文字コード自動判別を行うサービス
    /// Excel互換のShift_JISおよびBOM付き/なしUTF-8の双方向変換に対応します。
    /// </summary>
    public static class CsvService
    {
        static CsvService()
        {
            // 日本語Shift_JIS（CP932）文字コードプロバイダの登録
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        /// <summary>
        /// データをExcelで文字化けせずに直接開けるBOM付きUTF-8形式のCSVバイト配列として出力します。
        /// </summary>
        /// <typeparam name="T">データモデル型</typeparam>
        /// <param name="items">出力対象データリスト</param>
        /// <param name="headers">CSVヘッダー文字列配列</param>
        /// <param name="rowSelector">モデルからCSV各カラム文字列へのマッピング関数</param>
        /// <returns>BOM付きUTF-8エンコードされたバイト配列</returns>
        public static byte[] ExportToCsvBytes<T>(IEnumerable<T> items, string[] headers, Func<T, string[]> rowSelector)
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", headers));

            foreach (var item in items)
            {
                var values = rowSelector(item);
                var escapedValues = new string[values.Length];
                for (int i = 0; i < values.Length; i++)
                {
                    var val = values[i] ?? "";
                    // カンマ、ダブルクォーテーション、改行を含む場合はエスケープ処理
                    if (val.Contains(",") || val.Contains("\"") || val.Contains("\n") || val.Contains("\r"))
                    {
                        val = "\"" + val.Replace("\"", "\"\"") + "\"";
                    }
                    escapedValues[i] = val;
                }
                sb.AppendLine(string.Join(",", escapedValues));
            }

            var bom = Encoding.UTF8.GetPreamble(); // UTF-8 BOM (0xEF, 0xBB, 0xBF)
            var contentBytes = Encoding.UTF8.GetBytes(sb.ToString());
            
            var result = new byte[bom.Length + contentBytes.Length];
            Buffer.BlockCopy(bom, 0, result, 0, bom.Length);
            Buffer.BlockCopy(contentBytes, 0, result, bom.Length, contentBytes.Length);

            return result;
        }

        /// <summary>
        /// データをUTF-8形式のCSVテキスト文字列として出力します。
        /// </summary>
        /// <typeparam name="T">データモデル型</typeparam>
        /// <param name="items">出力対象データリスト</param>
        /// <param name="headers">CSVヘッダー文字列配列</param>
        /// <param name="rowSelector">モデルからCSV各カラム文字列へのマッピング関数</param>
        /// <returns>CSV文字列</returns>
        public static string ExportToCsv<T>(IEnumerable<T> items, string[] headers, Func<T, string[]> rowSelector)
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", headers));

            foreach (var item in items)
            {
                var values = rowSelector(item);
                var escapedValues = new string[values.Length];
                for (int i = 0; i < values.Length; i++)
                {
                    var val = values[i] ?? "";
                    if (val.Contains(",") || val.Contains("\"") || val.Contains("\n") || val.Contains("\r"))
                    {
                        val = "\"" + val.Replace("\"", "\"\"") + "\"";
                    }
                    escapedValues[i] = val;
                }
                sb.AppendLine(string.Join(",", escapedValues));
            }

            return sb.ToString();
        }

        /// <summary>
        /// アップロードされたCSVファイルを非同期で解析し、行ごとの文字列配列リストを返します。
        /// Shift_JISおよびUTF-8の文字コードを自動判別します。
        /// </summary>
        /// <param name="file">フォームファイル</param>
        /// <returns>解析されたCSV行（カラム文字列配列）のリスト</returns>
        public static async Task<List<string[]>> ReadCsvAsync(IFormFile file)
        {
            var list = new List<string[]>();
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var buffer = ms.ToArray();

            // エンコーディング判別（BOM付UTF-8 / UTF-8 / Shift_JIS）
            Encoding encoding = DetectEncoding(buffer);

            using var stream = new MemoryStream(buffer);
            using var reader = new StreamReader(stream, encoding);

            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = ParseCsvLine(line);
                list.Add(parts);
            }
            return list;
        }

        /// <summary>
        /// バイト配列から文字コード（UTF-8またはShift_JIS）を判定します。
        /// </summary>
        /// <param name="buffer">ファイルバイト配列</param>
        /// <returns>判定されたEncodingオブジェクト</returns>
        private static Encoding DetectEncoding(byte[] buffer)
        {
            // UTF-8 BOM判定
            if (buffer.Length >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
            {
                return Encoding.UTF8;
            }

            // BOMなしUTF-8の妥当性確認
            try
            {
                var utf8Strict = new UTF8Encoding(false, true);
                utf8Strict.GetString(buffer);
                return Encoding.UTF8;
            }
            catch
            {
                // 日本語Excel等の標準出力形式Shift_JIS (CP932)にフォールバック
                return Encoding.GetEncoding("shift_jis");
            }
        }

        /// <summary>
        /// CSVの1行文字列をダブルクォーテーションのエスケープ状態を考慮してパースします。
        /// </summary>
        /// <param name="line">CSVの1行</param>
        /// <returns>カラム文字列の配列</returns>
        private static string[] ParseCsvLine(string line)
        {
            var result = new List<string>();
            var inQuotes = false;
            var sb = new StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(sb.ToString().Trim());
                    sb.Clear();
                }
                else
                {
                    sb.Append(c);
                }
            }
            result.Add(sb.ToString().Trim());
            return result.ToArray();
        }
    }
}
