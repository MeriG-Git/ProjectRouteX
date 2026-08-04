using System;
using System.Text.RegularExpressions;

namespace RouteXWms.Helpers
{
    public static class ErrorHelper
    {
        /// <summary>
        /// 例外オブジェクトまたはメッセージをユーザーが直感的に理解できる日本語エラーメッセージに変換します。
        /// </summary>
        public static string ToUserFriendlyMessage(Exception ex)
        {
            if (ex == null) return "予期せぬエラーが発生しました。";

            // InnerException を再帰的に確認し最も深い詳細を取得
            string rawDetail = ex.InnerException?.InnerException?.Message 
                            ?? ex.InnerException?.Message 
                            ?? ex.Message 
                            ?? "";

            return ConvertRawMessageToJapanese(rawDetail);
        }

        public static string ConvertRawMessageToJapanese(string rawDetail)
        {
            if (string.IsNullOrWhiteSpace(rawDetail)) return "処理中にエラーが発生しました。";

            // EF Core リトライ戦略制限
            if (rawDetail.Contains("SqlServerRetryingExecutionStrategy does not support user-initiated transactions") ||
                rawDetail.Contains("SqlServerRetryingExecutionStrategy"))
            {
                return "データベースの接続リトライ制限が発生しました。大変お手数ですが、再度実行してください。";
            }

            // 主キー・一意制約違反 (SQL Error 2627 / 2601)
            if (rawDetail.Contains("PRIMARY KEY") || rawDetail.Contains("UNIQUE KEY") || 
                rawDetail.Contains("Cannot insert duplicate key") || rawDetail.Contains("一意なインデックス"))
            {
                return "指定されたキー（コード）は既にデータベースに登録されています。重複しないキーを指定してください。";
            }

            // 外部キー制約違反 (SQL Error 547)
            if (rawDetail.Contains("FOREIGN KEY constraint") || rawDetail.Contains("FK_"))
            {
                return "関連するマスターデータ（荷主、倉庫、商品、運送会社等）が存在しません。マスター登録を確認してください。";
            }

            // 数値・日付・型パースエラー
            if (rawDetail.Contains("Input string was not in a correct format") || rawDetail.Contains("String was not recognized as a valid DateTime"))
            {
                return "入力された数値または日付のフォーマットが不正です。正しい形式（例: yyyyMMdd や半角数字）で入力してください。";
            }

            // カラム不在・ヘッダー構造不正 (MiniExcel / CsvService)
            if (rawDetail.Contains("does not exist") || rawDetail.Contains("Column") || rawDetail.Contains("Header"))
            {
                return "CSV/Excelファイルの列構造（ヘッダー項目）がシステムのフォーマットと一致していません。ファイル仕様を確認してください。";
            }

            // NULL制約違反
            if (rawDetail.Contains("Cannot insert the value NULL into column"))
            {
                var match = Regex.Match(rawDetail, @"column '([^']+)'");
                string columnName = match.Success ? match.Groups[1].Value : "必須項目";
                return $"必須項目（{columnName}）の値が設定されていません。項目を入力してください。";
            }

            // 桁数オーバー
            if (rawDetail.Contains("String or binary data would be truncated"))
            {
                return "入力された文字列がデータベースの指定桁数を超えています。文字数を短くして再試行してください。";
            }

            // 日本語メッセージが既にセットされている場合はそのまま返す
            if (ContainsJapanese(rawDetail))
            {
                return rawDetail;
            }

            // その他の英文例外の場合の汎用日本語フォールバック
            return $"処理中にエラーが発生しました（詳細: {rawDetail}）";
        }

        private static bool ContainsJapanese(string text)
        {
            return Regex.IsMatch(text, @"[\u3000-\u303f\u3040-\u309f\u30a0-\u30ff\u4e00-\u9faf]");
        }
    }
}
