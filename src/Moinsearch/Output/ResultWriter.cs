using System.Text;
using Moinsearch.Search;

namespace Moinsearch.Output;

/// <summary>
/// 検索結果を「ページ名&lt;TAB&gt;URL」の1行1件で出力する。
/// ヘッダー・抜粋・件数・成功メッセージは出さない。0件なら何も出力しない。
/// </summary>
internal static class ResultWriter
{
    public static void Write(TextWriter writer, IEnumerable<SearchResult> results)
    {
        foreach (var result in results)
        {
            writer.Write(Sanitize(result.PageName));
            writer.Write('\t');
            writer.Write(Sanitize(result.Url));
            writer.Write(writer.NewLine);
        }
    }

    /// <summary>
    /// 改行・タブ・制御文字を置換し、TSV の行構造や端末表示を壊さないようにする。
    /// </summary>
    internal static string Sanitize(string value)
    {
        var hasControl = false;
        foreach (var c in value)
        {
            if (char.IsControl(c))
            {
                hasControl = true;
                break;
            }
        }

        if (!hasControl)
        {
            return value;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            builder.Append(char.IsControl(c) ? '\uFFFD' : c);
        }

        return builder.ToString();
    }
}
