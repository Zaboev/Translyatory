using System;
using System.IO;
using System.Text;

class CommentRemover
{
    static void Main(string[] args)
    {
        Console.WriteLine("Введите путь к файлу с маркерами комментариев:");
        string markersPath = Console.ReadLine();

        Console.WriteLine("Введите путь к входному файлу с кодом:");
        string inputPath = Console.ReadLine();

        Console.WriteLine("Введите путь для выходного файла:");
        string outputPath = Console.ReadLine();

        try
        {
            string[] markers = File.ReadAllLines(markersPath);
            if (markers.Length < 3)
                throw new Exception("Файл маркеров должен содержать 3 строки: начало однострочного, начало многострочного, конец многострочного.");

            string singleLineStart = markers[0];
            string multiLineStart = markers[1];
            string multiLineEnd = markers[2];

            string code = File.ReadAllText(inputPath);
            string cleanedCode = RemoveComments(code, singleLineStart, multiLineStart, multiLineEnd);
            File.WriteAllText(outputPath, cleanedCode);

            Console.WriteLine("Комментарии успешно удалены!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка: {ex.Message}");
        }
    }

    static string RemoveComments(
        string input,
        string singleLineStart,
        string multiLineStart,
        string multiLineEnd)
    {
        StringBuilder output = new StringBuilder();
        bool inSingleLineComment = false;
        bool inMultiLineComment = false;
        int i = 0;
        int length = input.Length;

        while (i < length)
        {       
            if (!inMultiLineComment && !inSingleLineComment &&
                StartsWith(input, i, singleLineStart))
            {
                inSingleLineComment = true;
                i += singleLineStart.Length;
                continue;
            }

            if (!inMultiLineComment && !inSingleLineComment &&
                StartsWith(input, i, multiLineStart))
            {
                inMultiLineComment = true;
                i += multiLineStart.Length;
                continue;
            }

            if (inMultiLineComment && StartsWith(input, i, multiLineEnd))
            {
                inMultiLineComment = false;
                i += multiLineEnd.Length;
                continue;
            }

            if (inSingleLineComment)
            {
                if (input[i] == '\n')
                {
                    inSingleLineComment = false;
                    output.Append('\n');
                }
                i++;
                continue;
            }

            if (inMultiLineComment)
            {
                i++;
                continue;
            }

            output.Append(input[i]);
            i++;
        }

        return output.ToString();
    }

    static bool StartsWith(string input, int i, string marker)
    {
        if (string.IsNullOrEmpty(marker))
            return false;

        if (i + marker.Length > input.Length)
            return false;

        for (int j = 0; j < marker.Length; j++)
        {
            if (input[i + j] != marker[j])
                return false;
        }
        return true;
    }
}
