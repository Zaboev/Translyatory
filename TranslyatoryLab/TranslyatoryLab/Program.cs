using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace LanguageInterpreter
{
    public class Interpreter
    {
        private Dictionary<string, Func<double[], double>> functions = new Dictionary<string, Func<double[], double>>();
        private Dictionary<string, double> variables = new Dictionary<string, double>();
        private Dictionary<string, char> variableTypes = new Dictionary<string, char>();

        public void Interpret(string[] lines)
        {
            foreach (var line in lines.Where(l => l.Contains(":") && !l.Contains("=")))
            {
                ProcessFunctionDefinition(line.Trim());
            }

            foreach (var line in lines.Where(l => !l.Contains(":") || l.Contains("=")))
            {
                if (string.IsNullOrWhiteSpace(line.Trim())) continue;
                ProcessLine(line.Trim());
            }
        }

        private void ProcessFunctionDefinition(string line)
        {
            var parts = line.Split(':');
            var header = parts[0].Trim();
            var body = parts[1].Trim().TrimEnd(';');

            var match = Regex.Match(header, @"^([a-zA-Z0-9]+)\(([a-zA-Z0-9, ]*)\)$");
            if (!match.Success) return;

            var funcName = match.Groups[1].Value;
            var parameters = match.Groups[2].Value
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .ToArray();

            functions[funcName] = args =>
            {
                if (args.Length != parameters.Length)
                    throw new Exception($"Функция {funcName} требует {parameters.Length} аргументов, получено {args.Length}");

                var locals = parameters.Zip(args, (k, v) => new { k, v })
                    .ToDictionary(x => x.k, x => x.v);

                return EvaluateExpression(body, locals);
            };
        }

        private void ProcessLine(string line)
        {
            if (line.StartsWith("print"))
            {
                PrintVariables(line);
            }
            else if (line.Contains("="))
            {
                AssignVariable(line);
            }
        }

        private void PrintVariables(string line)
        {
            var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1)
            {
                Console.WriteLine("Текущие переменные:");
                foreach (var kvp in variables)
                {
                    Console.WriteLine($"{kvp.Key} ({variableTypes[kvp.Key]}) = {kvp.Value.ToString(CultureInfo.InvariantCulture)}");
                }
            }
            else
            {
                var varName = parts[1].TrimEnd(';');
                if (variables.ContainsKey(varName))
                    Console.WriteLine($"{varName} = {variables[varName].ToString(CultureInfo.InvariantCulture)}");
                else
                    Console.WriteLine($"Переменная '{varName}' не определена");
            }
        }

        private void AssignVariable(string line)
        {
            var parts = line.Split('=');
            var left = parts[0].Trim();
            var right = parts[1].Trim().TrimEnd(';');

            char? type = null;
            string varName;

            var typeMatch = Regex.Match(left, @"^([a-zA-Z0-9]+)\(([if])\)$");
            if (typeMatch.Success)
            {
                varName = typeMatch.Groups[1].Value;
                type = typeMatch.Groups[2].Value[0];
            }
            else
            {
                varName = left;
            }

            double value = EvaluateExpression(right);

            if (type.HasValue)
            {
                variableTypes[varName] = type.Value;
                if (type.Value == 'i') value = Math.Round(value);
            }
            else if (!variableTypes.ContainsKey(varName))
            {
                variableTypes[varName] = 'f';
            }

            variables[varName] = value;
        }

        private double EvaluateExpression(string expr, Dictionary<string, double> locals = null)
        {
            expr = ProcessFunctionCalls(expr, locals); //Обработка вложенных функций

            expr = ReplaceVariables(expr, locals); // Замена переменных

            expr = NormalizeExpression(expr); // Нормализация выражения

            try // Вычисление выражения
            {
                return Convert.ToDouble(new System.Data.DataTable().Compute(expr, null));
            }
            catch
            {
                throw new Exception($"Ошибка вычисления выражения: {expr}");
            }
        }

        private string NormalizeExpression(string expr)
        {
            expr = Regex.Replace(expr, @"(\d),(\d)", "$1.$2"); // Замена запятых на точки для десятичных чисел

            expr = Regex.Replace(expr, @"(?<=^|[\s(])\-(\d)", " -$1"); // Обработка унарного минуса

            return expr.Replace(" ", ""); // Удаление лишних пробелов
        }

        private string ProcessFunctionCalls(string expr, Dictionary<string, double> locals)
        {
            int pos = 0;
            while (pos < expr.Length)
            {
                if (char.IsLetter(expr[pos]))
                {
                    int start = pos;
                    while (pos < expr.Length && (char.IsLetterOrDigit(expr[pos])))
                    {
                        pos++;
                    }

                    if (pos < expr.Length && expr[pos] == '(')
                    {
                        string funcName = expr.Substring(start, pos - start);
                        if (functions.ContainsKey(funcName))
                        {
                            int parenCount = 1;
                            int argsStart = pos + 1;
                            pos++;
                            while (pos < expr.Length && parenCount > 0)
                            {
                                if (expr[pos] == '(') parenCount++;
                                else if (expr[pos] == ')') parenCount--;
                                pos++;
                            }

                            string argsStr = expr.Substring(argsStart, pos - argsStart - 1);
                            var args = ParseArguments(argsStr, locals);
                            var result = functions[funcName](args);
                            expr = expr.Substring(0, start) + result.ToString(CultureInfo.InvariantCulture) + expr.Substring(pos);
                            pos = start + result.ToString(CultureInfo.InvariantCulture).Length;
                        }
                    }
                }
                else
                {
                    pos++;
                }
            }
            return expr;
        }

        private double[] ParseArguments(string argsStr, Dictionary<string, double> locals)
        {
            var args = new List<double>();
            int depth = 0;
            int start = 0;

            for (int i = 0; i < argsStr.Length; i++)
            {
                char c = argsStr[i];
                if (c == '(') depth++;
                else if (c == ')') depth--;
                else if (c == ',' && depth == 0)
                {
                    var arg = argsStr.Substring(start, i - start).Trim();
                    args.Add(EvaluateExpression(arg, locals));
                    start = i + 1;
                }
            }

            if (start < argsStr.Length) // Добавляем последний аргумент
            {
                var lastArg = argsStr.Substring(start).Trim();
                if (!string.IsNullOrEmpty(lastArg))
                    args.Add(EvaluateExpression(lastArg, locals));
            }

            return args.ToArray();
        }

        private string ReplaceVariables(string expr, Dictionary<string, double> locals)
        {
            return Regex.Replace(expr, @"(?<!\w)([a-zA-Z][a-zA-Z0-9]*)", match =>
            {
                var name = match.Groups[1].Value;

                if (functions.ContainsKey(name))
                    return name;

                if (locals != null && locals.ContainsKey(name))
                    return locals[name].ToString(CultureInfo.InvariantCulture);

                if (variables.ContainsKey(name))
                    return variables[name].ToString(CultureInfo.InvariantCulture);

                if (double.TryParse(name, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
                    return name;

                throw new Exception($"Неизвестная переменная: {name}");
            });
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Укажите файл с инструкциями как аргумент командной строки");
                return;
            }

            try
            {
                var lines = File.ReadAllLines(args[0]);
                new Interpreter().Interpret(lines);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
            }
        }
    }
}