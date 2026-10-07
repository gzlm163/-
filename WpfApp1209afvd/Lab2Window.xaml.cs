using NCalc;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace WpfApp1209afvd
{
    public partial class Lab2Window : Window
    {
        private const int MaxIterations = 10_000;
        private const int SubIntervals = 1000;
        private const double PlotMargin = 30;
        private const double DiscontinuityThreshold = 1e6;
        private const double RootSearchJumpLimit = 100;

        public Lab2Window()
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

            formula = formula.Replace("^", "**");

            try
            {
                var testExpr = new NCalc.Expression(formula);
                testExpr.Parameters["x"] = 1.0;
                var testResult = testExpr.Evaluate();

                if (testResult == null)
                {
                    error = "Не удалось вычислить формулу";
                    return false;
                }

                function = (double x) =>
                {
                    try
                    {
                        var expr = new NCalc.Expression(formula);
                        expr.Parameters["x"] = x;
                        var result = expr.Evaluate();

                        if (result == null)
                            return double.NaN;

                        return Convert.ToDouble(result);
                    }
                    catch
                    {
                        return double.NaN;
                    }
                };

                return true;
            }
            catch (Exception ex)
            {
                error = "Ошибка в формуле: " + ex.Message;
                return false;
            }
        }

        private bool TryParseDouble(string text, string fieldName, out double value, out string error)
        {
            value = 0;
            error = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                error = "Поле \"" + fieldName + "\" пустое";
                return false;
            }

            string cleaned = text.Trim().Replace('.', ',');

            if (!double.TryParse(cleaned, out value))
            {
                error = "Поле \"" + fieldName + "\" должно быть числом";
                return false;
            }

            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                error = "Поле \"" + fieldName + "\" не может быть NaN или бесконечностью";
                return false;
            }

            return true;
        }

        private bool IsDiscontinuity(double value)
        {
            return double.IsNaN(value) || double.IsInfinity(value) || Math.Abs(value) > DiscontinuityThreshold;
        }

        private bool TryBisection(Func<double, double> f, double a, double b, double e, out double root, out string error)
        {
            root = 0;
            error = null;

            double fa = f(a);
            double fb = f(b);

            if (IsDiscontinuity(fa) || IsDiscontinuity(fb))
            {
                error = "Функция имеет разрыв на концах отрезка";
                return false;
            }

            if (fa * fb > 0)
            {
                error = "На отрезке нет смены знака";
                return false;
            }

            if (Math.Abs(fa) < 1e-12)
            {
                root = a;
                return true;
            }

            if (Math.Abs(fb) < 1e-12)
            {
                root = b;
                return true;
            }

            int iterations = 0;

            while (b - a > e)
            {
                if (++iterations > MaxIterations)
                {
                    error = "Превышен лимит итераций";
                    return false;
                }

                double x = (a + b) / 2;
                double fx = f(x);

                if (IsDiscontinuity(fx))
                {
                    error = "Функция имеет разрыв внутри отрезка";
                    return false;
                }

                if (Math.Abs(fx) < 1e-12 || b - a <= e)
                {
                    root = x;
                    return true;
                }

                if (fa * fx < 0)
                {
                    b = x;
                    fb = fx;
                }
                else
                {
                    a = x;
                    fa = fx;
                }
            }

            root = (a + b) / 2;
            return true;
        }

        private List<double> FindAllRoots(Func<double, double> f, double a, double b, double e)
        {
            List<double> roots = new List<double>();

            double step = (b - a) / SubIntervals;
            double prevX = a;
            double prevFx = f(prevX);

            for (int i = 1; i <= SubIntervals; ++i)
            {
                double currentX = a + i * step;
                double currentFx = f(currentX);

                if (IsDiscontinuity(prevFx) || IsDiscontinuity(currentFx))
                {
                    prevX = currentX;
                    prevFx = currentFx;
                    continue;
                }

                if (prevFx * currentFx < 0 || Math.Abs(currentFx) < 1e-12)
                {
                    double midX = (prevX + currentX) / 2;
                    double midFx = f(midX);

                    if (double.IsNaN(midFx) || double.IsInfinity(midFx))
                    {
                        prevX = currentX;
                        prevFx = currentFx;
                        continue;
                    }

                    double maxAbs = Math.Max(Math.Abs(prevFx), Math.Abs(currentFx));
                    maxAbs = Math.Max(maxAbs, Math.Abs(midFx));

                    if (maxAbs > RootSearchJumpLimit)
                    {
                        prevX = currentX;
                        prevFx = currentFx;
                        continue;
                    }

                    if (TryBisection(f, prevX, currentX, e, out double root, out string bisectionError))
                    {
                        bool isDuplicate = false;

                        foreach (double existing in roots)
                        {
                            if (Math.Abs(existing - root) < e)
                            {
                                isDuplicate = true;
                                break;
                            }
                        }

                        if (!isDuplicate)
                        {
                            roots.Add(root);
                        }
                    }
                }

                prevX = currentX;
                prevFx = currentFx;
            }

            roots.Sort();
            return roots;
        }

        private void DrawPlot(Func<double, double> f, double a, double b, List<double> roots)
        {
            PlotCanvas.Children.Clear();

            double width = PlotCanvas.ActualWidth;
            double height = PlotCanvas.ActualHeight;

            if (width <= 2 * PlotMargin || height <= 2 * PlotMargin)
            {
                return;
            }

            int samples = 500;
            double[] xs = new double[samples + 1];
            double[] ys = new double[samples + 1];

            double minY = double.MaxValue;
            double maxY = double.MinValue;

            for (int i = 0; i <= samples; ++i)
            {
                double x = a + (b - a) * i / samples;
                double y = f(x);

                xs[i] = x;
                ys[i] = y;

                if (!double.IsNaN(y) && !double.IsInfinity(y) && Math.Abs(y) < DiscontinuityThreshold)
                {
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }

            if (minY > maxY)
            {
                minY = -1;
                maxY = 1;
            }

            double rangeY = maxY - minY;
            if (rangeY < 1e-9) rangeY = 1;
            minY -= rangeY * 0.1;
            maxY += rangeY * 0.1;

            double plotWidth = width - 2 * PlotMargin;
            double plotHeight = height - 2 * PlotMargin;

            Func<double, double> mapX = x => PlotMargin + (x - a) / (b - a) * plotWidth;
            Func<double, double> mapY = y => height - PlotMargin - (y - minY) / (maxY - minY) * plotHeight;

            double axisY = mapY(0);
            if (axisY < PlotMargin) axisY = PlotMargin;
            if (axisY > height - PlotMargin) axisY = height - PlotMargin;

            Line xAxis = new Line
            {
                X1 = PlotMargin,
                Y1 = axisY,
                X2 = width - PlotMargin,
                Y2 = axisY,
                Stroke = Brushes.Black,
                StrokeThickness = 2
            };
            PlotCanvas.Children.Add(xAxis);

            double axisX = mapX(0);
            if (axisX < PlotMargin) axisX = PlotMargin;
            if (axisX > width - PlotMargin) axisX = width - PlotMargin;

            Line yAxis = new Line
            {
                X1 = axisX,
                Y1 = PlotMargin,
                X2 = axisX,
                Y2 = height - PlotMargin,
                Stroke = Brushes.Black,
                StrokeThickness = 2
            };
            PlotCanvas.Children.Add(yAxis);

            AddLabel(a.ToString("F2"), mapX(a), height - PlotMargin + 5);
            AddLabel(b.ToString("F2"), mapX(b) - 30, height - PlotMargin + 5);

            Polyline currentPolyline = new Polyline
            {
                Stroke = new SolidColorBrush(Color.FromRgb(0xE9, 0x1E, 0x63)),
                StrokeThickness = 2
            };

            for (int i = 0; i <= samples; ++i)
            {
                double x = xs[i];
                double y = ys[i];

                if (IsDiscontinuity(y))
                {
                    if (currentPolyline.Points.Count > 0)
                    {
                        PlotCanvas.Children.Add(currentPolyline);
                        currentPolyline = new Polyline
                        {
                            Stroke = new SolidColorBrush(Color.FromRgb(0xE9, 0x1E, 0x63)),
                            StrokeThickness = 2
                        };
                    }
                    continue;
                }

                double px = mapX(x);
                double py = mapY(y);

                currentPolyline.Points.Add(new Point(px, py));
            }

            if (currentPolyline.Points.Count > 0)
            {
                PlotCanvas.Children.Add(currentPolyline);
            }

            foreach (double root in roots)
            {
                double px = mapX(root);
                double py = axisY;

                Ellipse dot = new Ellipse
                {
                    Width = 12,
                    Height = 12,
                    Fill = new SolidColorBrush(Color.FromRgb(0xAD, 0x14, 0x57)),
                    Stroke = Brushes.Black,
                    StrokeThickness = 1
                };

                Canvas.SetLeft(dot, px - 6);
                Canvas.SetTop(dot, py - 6);
                PlotCanvas.Children.Add(dot);
            }
        }

        private void AddLabel(string text, double x, double y)
        {
            TextBlock label = new TextBlock
            {
                Text = text,
                Foreground = Brushes.Black,
                FontWeight = FontWeights.Bold,
                FontSize = 11
            };

            Canvas.SetLeft(label, x);
            Canvas.SetTop(label, y);
            PlotCanvas.Children.Add(label);
        }

        private void ButtonPlot_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateInputs(out Func<double, double> f, out double a, out double b, out double epsilon))
            {
                return;
            }

            List<double> roots = FindAllRoots(f, a, b, epsilon);
            DrawPlot(f, a, b, roots);
        }

        private void ButtonFindRoots_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateInputs(out Func<double, double> f, out double a, out double b, out double epsilon))
            {
                return;
            }

            List<double> roots = FindAllRoots(f, a, b, epsilon);

            if (roots.Count == 0)
            {
                TextBoxRoots.Text = "Корни не найдены на отрезке [" + a + "; " + b + "]";
            }
            else
            {
                string result = "Найдено корней: " + roots.Count + Environment.NewLine;

                for (int i = 0; i < roots.Count; ++i)
                {
                    result += "x" + (i + 1) + " = " + roots[i].ToString("F6") + Environment.NewLine;
                }

                TextBoxRoots.Text = result;
            }

            DrawPlot(f, a, b, roots);
        }

        private bool ValidateInputs(out Func<double, double> f, out double a, out double b, out double epsilon)
        {
            f = null;
            a = 0;
            b = 0;
            epsilon = 0;

            if (!TryParseFunction(TextBoxFunction.Text, out f, out string funcError))
            {
                MessageBox.Show(funcError);
                return false;
            }

            if (!TryParseDouble(TextBoxA.Text, "a", out a, out string aError))
            {
                MessageBox.Show(aError);
                return false;
            }

            if (!TryParseDouble(TextBoxB.Text, "b", out b, out string bError))
            {
                MessageBox.Show(bError);
                return false;
            }

            if (!TryParseDouble(TextBoxE.Text, "e", out epsilon, out string eError))
            {
                MessageBox.Show(eError);
                return false;
            }

            if (a >= b)
            {
                MessageBox.Show("a должно быть меньше b");
                return false;
            }

            if (epsilon <= 0)
            {
                MessageBox.Show("Точность e должна быть больше 0");
                return false;
            }

            if (epsilon >= b - a)
            {
                MessageBox.Show("Точность e должна быть меньше длины отрезка (b - a)");
                return false;
            }

            return true;
        }

        private void ButtonClear_Click(object sender, RoutedEventArgs e)
        {
            TextBoxFunction.Text = string.Empty;
            TextBoxA.Text = string.Empty;
            TextBoxB.Text = string.Empty;
            TextBoxE.Text = "0,0001";
            TextBoxRoots.Text = string.Empty;
            PlotCanvas.Children.Clear();
        }

        private void ButtonExit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}