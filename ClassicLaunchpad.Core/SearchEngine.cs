using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace ClassicLaunchpad.Core
{
    public class SearchEngine : ISearchEngine
    {
        public SearchResult Query(string input, List<AppItem> pool)
        {
            var result = new SearchResult();
            if (string.IsNullOrWhiteSpace(input))
            {
                result.FilteredApps = new List<AppItem>(pool);
                return result;
            }

            input = input.Trim();

            // Check for system actions
            if (Enum.TryParse<SystemActionType>(input, true, out var actionType) && actionType != SystemActionType.None)
            {
                // Ensure the parsed enum value is defined and the input matches the named representation
                if (Enum.IsDefined(typeof(SystemActionType), actionType) &&
                    string.Equals(actionType.ToString(), input, StringComparison.OrdinalIgnoreCase))
                {
                    result.IsSystemAction = true;
                    result.SystemAction = actionType;
                    return result;
                }
            }

            // Also check lowercase mappings for system actions
            var actionLower = input.ToLowerInvariant();
            if (actionLower == "lock") { result.IsSystemAction = true; result.SystemAction = SystemActionType.Lock; return result; }
            if (actionLower == "sleep") { result.IsSystemAction = true; result.SystemAction = SystemActionType.Sleep; return result; }
            if (actionLower == "restart") { result.IsSystemAction = true; result.SystemAction = SystemActionType.Restart; return result; }
            if (actionLower == "shutdown") { result.IsSystemAction = true; result.SystemAction = SystemActionType.Shutdown; return result; }
            if (actionLower == "empty trash" || actionLower == "emptytrash") { result.IsSystemAction = true; result.SystemAction = SystemActionType.EmptyTrash; return result; }

            // Check if it is a mathematical expression (contains only digits, whitespace, and basic operators + - * / ( ) .)
            if (Regex.IsMatch(input, @"^[0-9+\-*/().\s]+$") && Regex.IsMatch(input, @"[0-9]"))
            {
                // Validate parentheses balance and empty parentheses
                int balance = 0;
                int maxDepth = 0;
                bool ok = true;
                for (int idx = 0; idx < input.Length; idx++)
                {
                    if (input[idx] == '(')
                    {
                        balance++;
                        if (balance > maxDepth) maxDepth = balance;
                    }
                    else if (input[idx] == ')') balance--;
                    if (balance < 0)
                    {
                        ok = false;
                        break;
                    }
                }
                if (balance != 0 || maxDepth > 500)
                {
                    ok = false;
                }

                if (ok && !input.Replace(" ", "").Contains("()"))
                {
                    try
                    {
                        double val = EvaluateExpression(input);
                        if (!double.IsNaN(val) && !double.IsInfinity(val))
                        {
                            result.IsMathExpression = true;
                            result.MathResult = val.ToString("0.####", CultureInfo.InvariantCulture);
                            return result;
                        }
                    }
                    catch
                    {
                        // If parsing fails, fall back to normal search
                    }
                }
            }

            // Normal search: filter pool by name case-insensitive
            result.FilteredApps = pool
                .Where(app => app.Name.Contains(input, StringComparison.OrdinalIgnoreCase))
                .ToList();

            return result;
        }

        private double EvaluateExpression(string expr)
        {
            expr = expr.Replace(" ", "");
            double val = ParseExpression(ref expr);
            if (expr.Length > 0)
            {
                throw new FormatException("Unconsumed characters in expression");
            }
            return val;
        }

        private double ParseExpression(ref string expr)
        {
            double val = ParseTerm(ref expr);
            while (expr.Length > 0)
            {
                char op = expr[0];
                if (op == '+' || op == '-')
                {
                    expr = expr.Substring(1);
                    double nextVal = ParseTerm(ref expr);
                    if (op == '+') val += nextVal;
                    else val -= nextVal;
                }
                else
                {
                    break;
                }
            }
            return val;
        }

        private double ParseTerm(ref string expr)
        {
            double val = ParseFactor(ref expr);
            while (expr.Length > 0)
            {
                char op = expr[0];
                if (op == '*' || op == '/')
                {
                    expr = expr.Substring(1);
                    double nextVal = ParseFactor(ref expr);
                    if (op == '*') val *= nextVal;
                    else
                    {
                        if (nextVal == 0) throw new DivideByZeroException();
                        val /= nextVal;
                    }
                }
                else
                {
                    break;
                }
            }
            return val;
        }

        private double ParseFactor(ref string expr)
        {
            if (expr.Length == 0)
            {
                throw new FormatException("Empty factor");
            }

            if (expr[0] == '(')
            {
                expr = expr.Substring(1); // Consume '('
                double val = ParseExpression(ref expr);
                if (expr.Length > 0 && expr[0] == ')')
                {
                    expr = expr.Substring(1); // Consume ')'
                }
                else
                {
                    throw new FormatException("Missing closing parenthesis");
                }
                return val;
            }

            int i = 0;
            if (expr[0] == '-' || expr[0] == '+') i++;

            int digitCount = 0;
            while (i < expr.Length && (char.IsDigit(expr[i]) || expr[i] == '.'))
            {
                if (char.IsDigit(expr[i])) digitCount++;
                i++;
            }

            if (i == 0 || ((expr[0] == '-' || expr[0] == '+') && digitCount == 0))
            {
                throw new FormatException("Invalid number format");
            }

            string numStr = expr.Substring(0, i);
            expr = expr.Substring(i);
            return double.Parse(numStr, CultureInfo.InvariantCulture);
        }
    }
}
