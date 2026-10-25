using NCalc;
using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace WpfApp1209afvd
{
    public partial class Lab3Window : Window
    {
        private const int MaxIterations = 10_000;
        private const double PlotMargin = 30;

        public Lab3Window()
        {
            InitializeComponent();
        }

        private bool TryParseFunction(string formula, out Func<double, double> function, out string error)
        {
            function = null;
            error = null;

            if (string.IsNullOrWhiteSpace(formula))
            {
                error = "Формула не введена";
                return false;
            }

            string preparedFormula = formula.Replace("^", "**");
            string[] mathFunctions = { "sin", "cos", "tan", "asin", "acos", "atan", "sqrt", "abs", "exp", "log", "ln" };

            foreach (string func in mathFunctions)
            {
                string properName = char.ToUpper(func[0]) + func.Substring(1);
                preparedFormula = Regex.Replace(preparedFormula, @"\b" + func + @"\b", properName, RegexOptions.IgnoreCase);
            }

            try
            {
                var testExpression = new NCalc.Expression(preparedFormula);
                testExpression.Parameters["x"] = 1.0;

                if (testExpression.Evaluate() == null)
                {
                    error = "Ошибка вычисления формулы";
                    return false;
                }

                function = (double argumentX) =>
                {
                    try
                    {
                        var expression = new NCalc.Expression(preparedFormula);
                        expression.Parameters["x"] = argumentX;
                        var evaluationResult = expression.Evaluate();
                        return evaluationResult == null ? double.NaN : Convert.ToDouble(evaluationResult);
                    }
                    catch { return double.NaN; }
                };

                return true;
            }
            catch (Exception ex)
            {
                error = "Синтаксическая ошибка: " + ex.Message;
                return false;
            }
        }

        private bool TryParseDouble(string inputText, string fieldName, out double parsedValue, out string error)
        {
            parsedValue = 0;
            error = null;

            if (string.IsNullOrWhiteSpace(inputText))
            {
                error = "Поле \"" + fieldName + "\" пустое";
                return false;
            }

            string cleanedText = inputText.Trim().Replace('.', ',');

            if (!double.TryParse(cleanedText, out parsedValue))
            {
                error = "Поле \"" + fieldName + "\" не является числом";
                return false;
            }

            return true;
        }

        // --- МЕТОД ЗОЛОТОГО СЕЧЕНИЯ ---
        private bool TryGoldenSection(Func<double, double> function, double leftBoundary, double rightBoundary, double accuracy, bool findMaximum, out double extremumX, out double extremumY, out string error)
        {
            extremumX = 0;
            extremumY = 0;
            error = null;

            Func<double, double> targetFunction = findMaximum ? (x => -function(x)) : function;

            double phi = (1 + Math.Sqrt(5)) / 2; // 1.618...
            double resphi = 2 - phi;             // 0.3819...

            double x1 = leftBoundary + resphi * (rightBoundary - leftBoundary);
            double x2 = rightBoundary - resphi * (rightBoundary - leftBoundary);
            double f1 = targetFunction(x1);
            double f2 = targetFunction(x2);

            int iterationCount = 0;

            while (Math.Abs(rightBoundary - leftBoundary) > accuracy)
            {
                if (++iterationCount > MaxIterations)
                {
                    error = "Превышен лимит итераций";
                    return false;
                }

                if (f1 < f2)
                {
                    rightBoundary = x2;
                    x2 = x1;
                    f2 = f1;
                    x1 = leftBoundary + resphi * (rightBoundary - leftBoundary);
                    f1 = targetFunction(x1);
                }
                else
                {
                    leftBoundary = x1;
                    x1 = x2;
                    f1 = f2;
                    x2 = rightBoundary - resphi * (rightBoundary - leftBoundary);
                    f2 = targetFunction(x2);
                }
            }

            extremumX = (leftBoundary + rightBoundary) / 2;
            extremumY = function(extremumX);
            return true;
        }

        // --- МЕТОД ДИХОТОМИИ ДЛЯ КОРНЯ ---
        private bool TryBisection(Func<double, double> function, double leftBoundary, double rightBoundary, double accuracy, out double root, out string error)
        {
            root = 0;
            error = null;

            double valueAtLeft = function(leftBoundary);
            double valueAtRight = function(rightBoundary);

            if (valueAtLeft * valueAtRight >= 0)
            {
                error = "На концах отрезка функция не меняет знак";
                return false;
            }

            int iterationCount = 0;

            while (rightBoundary - leftBoundary > accuracy)
            {
                if (++iterationCount > MaxIterations)
                {
                    error = "Превышен лимит итераций";
                    return false;
                }

                double middlePoint = (leftBoundary + rightBoundary) / 2;
                double valueAtMiddle = function(middlePoint);

                if (double.IsNaN(valueAtMiddle))
                {
                    error = "Функция не определена в точке";
                    return false;
                }

                if (valueAtLeft * valueAtMiddle < 0)
                {
                    rightBoundary = middlePoint;
                    valueAtRight = valueAtMiddle;
                }
                else
                {
                    leftBoundary = middlePoint;
                    valueAtLeft = valueAtMiddle;
                }
            }

            root = (leftBoundary + rightBoundary) / 2;
            return true;
        }

        private void DrawPlot(Func<double, double> function, double leftBoundary, double rightBoundary, double? specialPointX, string pointType)
        {
            PlotCanvas.Children.Clear();

            double canvasWidth = PlotCanvas.ActualWidth;
            double canvasHeight = PlotCanvas.ActualHeight;

            if (canvasWidth <= 2 * PlotMargin || canvasHeight <= 2 * PlotMargin) return;

            int pointCount = 500;
            double[] plotX = new double[pointCount + 1];
            double[] plotY = new double[pointCount + 1];

            double minValueY = double.MaxValue;
            double maxValueY = double.MinValue;

            for (int pointIndex = 0; pointIndex <= pointCount; ++pointIndex)
            {
                double currentX = leftBoundary + (rightBoundary - leftBoundary) * pointIndex / pointCount;
                double currentY = function(currentX);

                plotX[pointIndex] = currentX;
                plotY[pointIndex] = currentY;

                if (!double.IsNaN(currentY) && !double.IsInfinity(currentY) && Math.Abs(currentY) < 1e6)
                {
                    if (currentY < minValueY) minValueY = currentY;
                    if (currentY > maxValueY) maxValueY = currentY;
                }
            }

            if (minValueY > maxValueY) { minValueY = -1; maxValueY = 1; }
            double valueRangeY = maxValueY - minValueY;
            if (valueRangeY < 1e-9) valueRangeY = 1;
            minValueY -= valueRangeY * 0.1;
            maxValueY += valueRangeY * 0.1;

            double plottingWidth = canvasWidth - 2 * PlotMargin;
            double plottingHeight = canvasHeight - 2 * PlotMargin;

            Func<double, double> mapToCanvasX = mathX => PlotMargin + (mathX - leftBoundary) / (rightBoundary - leftBoundary) * plottingWidth;
            Func<double, double> mapToCanvasY = mathY => canvasHeight - PlotMargin - (mathY - minValueY) / (maxValueY - minValueY) * plottingHeight;

            double axisYPixel = mapToCanvasY(0);
            if (axisYPixel < PlotMargin) axisYPixel = PlotMargin;
            if (axisYPixel > canvasHeight - PlotMargin) axisYPixel = canvasHeight - PlotMargin;

            Line xAxisLine = new Line { X1 = PlotMargin, Y1 = axisYPixel, X2 = canvasWidth - PlotMargin, Y2 = axisYPixel, Stroke = Brushes.Black, StrokeThickness = 2 };
            PlotCanvas.Children.Add(xAxisLine);

            double axisXPixel = mapToCanvasX(0);
            if (axisXPixel < PlotMargin) axisXPixel = PlotMargin;
            if (axisXPixel > canvasWidth - PlotMargin) axisXPixel = canvasWidth - PlotMargin;

            Line yAxisLine = new Line { X1 = axisXPixel, Y1 = PlotMargin, X2 = axisXPixel, Y2 = canvasHeight - PlotMargin, Stroke = Brushes.Black, StrokeThickness = 2 };
            PlotCanvas.Children.Add(yAxisLine);

            AddAxisLabel(leftBoundary.ToString("F2"), mapToCanvasX(leftBoundary), canvasHeight - PlotMargin + 5);
            AddAxisLabel(rightBoundary.ToString("F2"), mapToCanvasX(rightBoundary) - 30, canvasHeight - PlotMargin + 5);

            Polyline functionLine = new Polyline { Stroke = new SolidColorBrush(Color.FromRgb(0x8E, 0x24, 0xAA)), StrokeThickness = 2 };

            for (int pointIndex = 0; pointIndex <= pointCount; ++pointIndex)
            {
                double currentX = plotX[pointIndex];
                double currentY = plotY[pointIndex];

                if (double.IsNaN(currentY) || double.IsInfinity(currentY) || Math.Abs(currentY) > 1e6)
                {
                    if (functionLine.Points.Count > 0)
                    {
                        PlotCanvas.Children.Add(functionLine);
                        functionLine = new Polyline { Stroke = new SolidColorBrush(Color.FromRgb(0x8E, 0x24, 0xAA)), StrokeThickness = 2 };
                    }
                    continue;
                }

                functionLine.Points.Add(new Point(mapToCanvasX(currentX), mapToCanvasY(currentY)));
            }

            if (functionLine.Points.Count > 0) PlotCanvas.Children.Add(functionLine);

            if (specialPointX.HasValue)
            {
                double pixelX = mapToCanvasX(specialPointX.Value);
                double pixelY = mapToCanvasY(function(specialPointX.Value));

                Ellipse marker = new Ellipse { Width = 12, Height = 12, Fill = new SolidColorBrush(Color.FromRgb(0x4A, 0x14, 0x8C)), Stroke = Brushes.White, StrokeThickness = 1 };
                Canvas.SetLeft(marker, pixelX - 6);
                Canvas.SetTop(marker, pixelY - 6);
                PlotCanvas.Children.Add(marker);

                TextBlock label = new TextBlock { Text = pointType, Foreground = Brushes.Black, FontWeight = FontWeights.Bold, FontSize = 12 };
                Canvas.SetLeft(label, pixelX + 10);
                Canvas.SetTop(label, pixelY - 10);
                PlotCanvas.Children.Add(label);
            }
        }

        private void AddAxisLabel(string text, double xPosition, double yPosition)
        {
            TextBlock axisLabel = new TextBlock { Text = text, Foreground = Brushes.Black, FontWeight = FontWeights.Bold, FontSize = 11 };
            Canvas.SetLeft(axisLabel, xPosition);
            Canvas.SetTop(axisLabel, yPosition);
            PlotCanvas.Children.Add(axisLabel);
        }

        private void ButtonPlot_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateInputs(out Func<double, double> function, out double leftBoundary, out double rightBoundary, out double accuracy)) return;
            DrawPlot(function, leftBoundary, rightBoundary, null, "");
        }

        private void ButtonFindExtremum_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateInputs(out Func<double, double> function, out double leftBoundary, out double rightBoundary, out double accuracy)) return;

            bool isMax = RadioMax.IsChecked == true;

            if (TryGoldenSection(function, leftBoundary, rightBoundary, accuracy, isMax, out double extremumX, out double extremumY, out string error))
            {
                string type = isMax ? "Максимум" : "Минимум";
                TextBoxResult.Text = $"{type}:\r\nx = {extremumX:F6}\r\ny = {extremumY:F6}";
                DrawPlot(function, leftBoundary, rightBoundary, extremumX, type);
            }
            else
            {
                TextBoxResult.Text = error;
                DrawPlot(function, leftBoundary, rightBoundary, null, "");
            }
        }

        private void ButtonFindRoot_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateInputs(out Func<double, double> function, out double leftBoundary, out double rightBoundary, out double accuracy)) return;

            if (TryBisection(function, leftBoundary, rightBoundary, accuracy, out double root, out string error))
            {
                TextBoxResult.Text = $"Корень:\r\nx = {root:F6}";
                DrawPlot(function, leftBoundary, rightBoundary, root, "Корень");
            }
            else
            {
                TextBoxResult.Text = error;
                DrawPlot(function, leftBoundary, rightBoundary, null, "");
            }
        }

        private bool ValidateInputs(out Func<double, double> function, out double leftBoundary, out double rightBoundary, out double accuracy)
        {
            function = null; leftBoundary = 0; rightBoundary = 0; accuracy = 0;

            if (!TryParseFunction(TextBoxFunction.Text, out function, out string formulaError)) { MessageBox.Show(formulaError); return false; }
            if (!TryParseDouble(TextBoxA.Text, "a", out leftBoundary, out string aError)) { MessageBox.Show(aError); return false; }
            if (!TryParseDouble(TextBoxB.Text, "b", out rightBoundary, out string bError)) { MessageBox.Show(bError); return false; }

            if (!int.TryParse(TextBoxE.Text, out int decimalPlaces)) { MessageBox.Show("Количество знаков целое число"); return false; }
            if (decimalPlaces <= 0) { MessageBox.Show("Количество знаков > 0"); return false; }

            accuracy = 1.0 / Math.Pow(10, decimalPlaces);

            if (leftBoundary >= rightBoundary) { MessageBox.Show("a должно быть меньше b"); return false; }
            if (accuracy >= rightBoundary - leftBoundary) { MessageBox.Show("Слишком высокая точность"); return false; }

            return true;
        }

        private void ButtonClear_Click(object sender, RoutedEventArgs e)
        {
            TextBoxFunction.Text = string.Empty;
            TextBoxA.Text = string.Empty;
            TextBoxB.Text = string.Empty;
            TextBoxE.Text = string.Empty;
            TextBoxResult.Text = string.Empty;
            PlotCanvas.Children.Clear();
        }

        private void ButtonExit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}