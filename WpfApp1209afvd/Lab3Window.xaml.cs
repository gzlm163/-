using NCalc;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace WpfApp1209afvd
{
    public partial class Lab3Window : Window
    {
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
                        var result = expression.Evaluate();
                        return result == null ? double.NaN : Convert.ToDouble(result);
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

        private List<(double left, double right)> FindAllIsolationIntervals(Func<double, double> function, double start, double end, bool findMax)
        {
            List<(double left, double right)> intervals = new List<(double, double)>();
            double step = (end - start) / 200.0;

            double previousX = start;
            double previousFx = function(previousX);
            double currentX = start + step;
            double currentFx = function(currentX);

            while (currentX <= end)
            {
                double nextX = currentX + step;
                if (nextX > end) break;
                double nextFx = function(nextX);

                bool isExtremum = false;

                if (findMax)
                {
                    if (previousFx < currentFx && currentFx > nextFx) isExtremum = true;
                }
                else
                {
                    if (previousFx > currentFx && currentFx < nextFx) isExtremum = true;
                }

                if (isExtremum)
                {
                    intervals.Add((previousX, nextX));
                }

                previousX = currentX;
                previousFx = currentFx;
                currentX = nextX;
                currentFx = nextFx;
            }

            return intervals;
        }

        private double GoldenSectionSearch(Func<double, double> function, double left, double right, double accuracy, bool findMax)
        {
            double alpha = (3.0 - Math.Sqrt(5.0)) / 2.0;

            double x1 = left + alpha * (right - left);
            double x2 = right - alpha * (right - left);

            Func<double, double> targetFunction = findMax ? (x => -function(x)) : function;

            double f1 = targetFunction(x1);
            double f2 = targetFunction(x2);

            while (Math.Abs(right - left) > accuracy)
            {
                if (f1 < f2)
                {
                    right = x2;
                    x2 = x1;
                    f2 = f1;
                    x1 = left + alpha * (right - left);
                    f1 = targetFunction(x1);
                }
                else
                {
                    left = x1;
                    x1 = x2;
                    f1 = f2;
                    x2 = right - alpha * (right - left);
                    f2 = targetFunction(x2);
                }
            }

            return (left + right) / 2;
        }

        private double FindRoot(Func<double, double> function, double left, double right, double accuracy)
        {
            return GoldenSectionSearch(x => Math.Abs(function(x)), left, right, accuracy, false);
        }

        private void DrawPlot(Func<double, double> function, double left, double right, double? specialPointX, string pointType)
        {
            PlotCanvas.Children.Clear();

            double canvasWidth = PlotCanvas.ActualWidth;
            double canvasHeight = PlotCanvas.ActualHeight;
            if (canvasWidth <= 2 * PlotMargin || canvasHeight <= 2 * PlotMargin) return;

            int pointCount = 500;
            double minValueY = double.MaxValue;
            double maxValueY = double.MinValue;

            for (int pointIndex = 0; pointIndex <= pointCount; ++pointIndex)
            {
                double currentX = left + (right - left) * pointIndex / pointCount;
                double currentY = function(currentX);

                if (!double.IsNaN(currentY) && !double.IsInfinity(currentY) && Math.Abs(currentY) < 1e6)
                {
                    if (currentY < minValueY) minValueY = currentY;
                    if (currentY > maxValueY) maxValueY = currentY;
                }
            }

            if (minValueY > maxValueY) { minValueY = -1; maxValueY = 1; }
            double rangeY = maxValueY - minValueY;
            if (rangeY < 1e-9) rangeY = 1;
            minValueY -= rangeY * 0.1;
            maxValueY += rangeY * 0.1;

            double plotWidth = canvasWidth - 2 * PlotMargin;
            double plotHeight = canvasHeight - 2 * PlotMargin;

            Func<double, double> mapToCanvasX = mathX => PlotMargin + (mathX - left) / (right - left) * plotWidth;
            Func<double, double> mapToCanvasY = mathY => canvasHeight - PlotMargin - (mathY - minValueY) / (maxValueY - minValueY) * plotHeight;

            double axisYPixel = mapToCanvasY(0);
            if (axisYPixel < PlotMargin) axisYPixel = PlotMargin;
            if (axisYPixel > canvasHeight - PlotMargin) axisYPixel = canvasHeight - PlotMargin;

            double axisXPixel = mapToCanvasX(0);
            if (axisXPixel < PlotMargin) axisXPixel = PlotMargin;
            if (axisXPixel > canvasWidth - PlotMargin) axisXPixel = canvasWidth - PlotMargin;

            Line xAxisLine = new Line { X1 = PlotMargin, Y1 = axisYPixel, X2 = canvasWidth - PlotMargin - 10, Y2 = axisYPixel, Stroke = Brushes.Black, StrokeThickness = 2 };
            PlotCanvas.Children.Add(xAxisLine);
            Polygon xArrow = new Polygon { Fill = Brushes.Black, Points = new PointCollection { new Point(canvasWidth - PlotMargin, axisYPixel), new Point(canvasWidth - PlotMargin - 10, axisYPixel - 5), new Point(canvasWidth - PlotMargin - 10, axisYPixel + 5) } };
            PlotCanvas.Children.Add(xArrow);

            Line yAxisLine = new Line { X1 = axisXPixel, Y1 = canvasHeight - PlotMargin, X2 = axisXPixel, Y2 = PlotMargin + 10, Stroke = Brushes.Black, StrokeThickness = 2 };
            PlotCanvas.Children.Add(yAxisLine);
            Polygon yArrow = new Polygon { Fill = Brushes.Black, Points = new PointCollection { new Point(axisXPixel, PlotMargin), new Point(axisXPixel - 5, PlotMargin + 10), new Point(axisXPixel + 5, PlotMargin + 10) } };
            PlotCanvas.Children.Add(yArrow);

            AddAxisLabel("X", canvasWidth - PlotMargin + 5, axisYPixel - 10);
            AddAxisLabel("Y", axisXPixel + 5, PlotMargin);

            Func<double, double> getNiceStep = (range) =>
            {
                double rawStep = range / 8.0;
                double magnitude = Math.Pow(10, Math.Floor(Math.Log10(rawStep)));
                double normalizedStep = rawStep / magnitude;

                if (normalizedStep < 1.5) return magnitude;
                if (normalizedStep < 3.0) return 2 * magnitude;
                if (normalizedStep < 7.0) return 5 * magnitude;
                return 10 * magnitude;
            };

            double stepX = getNiceStep(right - left);
            double startX = Math.Ceiling(left / stepX) * stepX;
            for (double tickX = startX; tickX <= right; tickX += stepX)
            {
                double px = mapToCanvasX(tickX);
                Line tickLine = new Line { X1 = px, Y1 = axisYPixel - 3, X2 = px, Y2 = axisYPixel + 3, Stroke = Brushes.Black, StrokeThickness = 1 };
                PlotCanvas.Children.Add(tickLine);
                AddAxisLabel(tickX.ToString("G"), px - 10, axisYPixel + 5);
            }

            double stepY = getNiceStep(maxValueY - minValueY);
            double startY = Math.Ceiling(minValueY / stepY) * stepY;
            for (double tickY = startY; tickY <= maxValueY; tickY += stepY)
            {
                double py = mapToCanvasY(tickY);
                Line tickLine = new Line { X1 = axisXPixel - 3, Y1 = py, X2 = axisXPixel + 3, Y2 = py, Stroke = Brushes.Black, StrokeThickness = 1 };
                PlotCanvas.Children.Add(tickLine);

                if (Math.Abs(tickY) > stepY / 10)
                {
                    var label = new TextBlock { Text = tickY.ToString("G"), FontSize = 11, FontWeight = FontWeights.Bold };
                    label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    double textWidth = label.DesiredSize.Width;
                    double labelX = axisXPixel < 40 ? axisXPixel + 8 : axisXPixel - textWidth - 5;
                    AddAxisLabel(tickY.ToString("G"), labelX, py - 8);
                }
            }

            Polyline functionLine = new Polyline { Stroke = new SolidColorBrush(Color.FromRgb(0x8E, 0x24, 0xAA)), StrokeThickness = 2 };

            for (int pointIndex = 0; pointIndex <= pointCount; ++pointIndex)
            {
                double currentX = left + (right - left) * pointIndex / pointCount;
                double currentY = function(currentX);

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

            var intervals = FindAllIsolationIntervals(function, leftBoundary, rightBoundary, isMax);

            if (intervals.Count == 0)
            {
                string type = isMax ? "максимумов" : "минимумов";
                TextBoxResult.Text = $"На отрезке нет {type}";
                DrawPlot(function, leftBoundary, rightBoundary, null, "");
                return;
            }

            var targetInterval = intervals[0];

            double extremumX = GoldenSectionSearch(function, targetInterval.left, targetInterval.right, accuracy, isMax);
            double extremumY = function(extremumX);

            string resultType = isMax ? "Максимум" : "Минимум";
            TextBoxResult.Text = $"{resultType}:\r\nx = {extremumX:F6}\r\ny = {extremumY:F6}";
            DrawPlot(function, leftBoundary, rightBoundary, extremumX, resultType);
        }

        private void ButtonFindRoot_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateInputs(out Func<double, double> function, out double leftBoundary, out double rightBoundary, out double accuracy)) return;

            int partsCount = 100;
            double step = (rightBoundary - leftBoundary) / partsCount;
            double foundRoot = double.NaN;
            bool rootFound = false;

            for (int partIndex = 0; partIndex < partsCount; partIndex++)
            {
                double currentLeft = leftBoundary + partIndex * step;
                double currentRight = currentLeft + step;

                double leftValue = function(currentLeft);
                double rightValue = function(currentRight);

                if (double.IsNaN(leftValue) || double.IsNaN(rightValue)) continue;
                if (Math.Abs(leftValue) > 1000000 || Math.Abs(rightValue) > 1000000) continue;

                bool signChanged = leftValue * rightValue < 0;
                bool nearZero = Math.Abs(leftValue) < 1e-5 || Math.Abs(rightValue) < 1e-5;

                if (signChanged || nearZero)
                {
                    foundRoot = FindRoot(function, currentLeft, currentRight, accuracy);

                    double rootValue = function(foundRoot);
                    if (Math.Abs(rootValue) < accuracy)
                    {
                        rootFound = true;
                        break;
                    }
                }
            }

            if (!rootFound)
            {
                TextBoxResult.Text = "Корень не найден (функция не пересекает ноль)";
                DrawPlot(function, leftBoundary, rightBoundary, null, "");
                return;
            }

            double rootY = function(foundRoot);
            TextBoxResult.Text = $"Корень:\r\nx = {foundRoot:F6}\r\ny = {rootY:F6}";
            DrawPlot(function, leftBoundary, rightBoundary, foundRoot, "Корень");
        }

        private bool ValidateInputs(out Func<double, double> function, out double leftBoundary, out double rightBoundary, out double accuracy)
        {
            function = null; leftBoundary = 0; rightBoundary = 0; accuracy = 0;

            if (!TryParseFunction(TextBoxFunction.Text, out function, out string formulaError)) { MessageBox.Show(formulaError); return false; }
            if (!TryParseDouble(TextBoxA.Text, "a", out leftBoundary, out string leftError)) { MessageBox.Show(leftError); return false; }
            if (!TryParseDouble(TextBoxB.Text, "b", out rightBoundary, out string rightError)) { MessageBox.Show(rightError); return false; }

            if (!int.TryParse(TextBoxE.Text, out int decimalPlaces)) { MessageBox.Show("Количество знаков должно быть целым числом"); return false; }
            if (decimalPlaces <= 0) { MessageBox.Show("Количество знаков должно быть больше 0"); return false; }

            accuracy = 1.0 / Math.Pow(10, decimalPlaces);

            if (leftBoundary >= rightBoundary) { MessageBox.Show("a должно быть меньше b"); return false; }
            if (accuracy >= rightBoundary - leftBoundary) { MessageBox.Show("Слишком высокая точность для данного отрезка"); return false; }

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