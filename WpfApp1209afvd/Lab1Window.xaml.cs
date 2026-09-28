using ClosedXML.Excel;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;

namespace WpfApp1209afvd
{
    public partial class Lab1Window : Window
    {
        private const int VisualizationLimit = 20;
        private const int MaxArraySize = 100_000;
        private const int RoundingDigits = 2;
        private const int DefaultBogoLimit = 1000;
        private const int MaxBogoLimit = 1_000_000;

        public Lab1Window()
        {
            InitializeComponent();
            DataGridManualInput.ItemsSource = new ObservableCollection<ManualNumber>();
        }

        public class ManualNumber
        {
            public string Value { get; set; }
        }

        public class SortResult
        {
            public string Name { get; set; }
            public int Iterations { get; set; }
            public string Time { get; set; }
        }

        private string FormatArray(List<double> numbers)
        {
            return string.Join(" ", numbers);
        }

        private void ResetSortResults()
        {
            DataGridResults.ItemsSource = null;
            TextBoxSortedArray.Text = string.Empty;
        }

        private bool NeedSwap(double firstValue, double secondValue, bool ascending)
        {
            return ascending ? firstValue > secondValue : firstValue < secondValue;
        }

        private List<double> ParseArrayFromTextBox(string text)
        {
            List<double> result = new List<double>();

            if (string.IsNullOrWhiteSpace(text))
            {
                return result;
            }

            string[] parts = text.Split(new char[] { ' ', '\t' },
                                         StringSplitOptions.RemoveEmptyEntries);

            foreach (string part in parts)
            {
                string cleanedPart = part.Trim().Replace('.', ',');

                if (double.TryParse(cleanedPart, out double value))
                {
                    if (double.IsNaN(value) || double.IsInfinity(value))
                    {
                        continue;
                    }

                    result.Add(Math.Round(value, RoundingDigits));
                }
            }

            return result;
        }

        private void ButtonCalculate_Click(object sender, RoutedEventArgs e)
        {
            List<double> sourceNumbers = ParseArrayFromTextBox(TextBoxInputArray.Text);

            if (sourceNumbers.Count == 0)
            {
                MessageBox.Show("Сначала сгенерируйте массив");
                return;
            }

            bool ascending = RadioAscending.IsChecked == true;

            List<SortResult> results = new List<SortResult>();
            List<double> visualizationSource = null;

            if (CheckBubble.IsChecked == true)
            {
                var (sorted, iterations) = MeasureSort("Пузырьковая", sourceNumbers, ascending, BubbleSort, ref results);
                visualizationSource = sorted;
            }

            if (CheckInsertion.IsChecked == true)
            {
                var (sorted, iterations) = MeasureSort("Вставками", sourceNumbers, ascending, InsertionSort, ref results);
                visualizationSource = sorted;
            }

            if (CheckShaker.IsChecked == true)
            {
                var (sorted, iterations) = MeasureSort("Шейкерная", sourceNumbers, ascending, ShakerSort, ref results);
                visualizationSource = sorted;
            }

            if (CheckQuick.IsChecked == true)
            {
                var (sorted, iterations) = MeasureSort("Быстрая", sourceNumbers, ascending, QuickSort, ref results);
                visualizationSource = sorted;
            }

            if (CheckBogo.IsChecked == true)
            {
                if (!int.TryParse(TextBoxBogoLimit.Text, out int bogoLimit))
                {
                    MessageBox.Show("Введите корректный лимит итераций для Bogo");
                    return;
                }

                if (bogoLimit < 0)
                {
                    MessageBox.Show("Лимит итераций для Bogo не может быть отрицательным");
                    return;
                }

                if (bogoLimit > MaxBogoLimit)
                {
                    MessageBox.Show("Лимит итераций для Bogo не должен превышать " + MaxBogoLimit);
                    return;
                }

                Stopwatch stopwatch = Stopwatch.StartNew();
                var (sorted, iterations, completed) = BogoSort(sourceNumbers, ascending, bogoLimit);
                stopwatch.Stop();

                string bogoName = completed ? "Bogo" : "Bogo (не завершена)";

                results.Add(new SortResult
                {
                    Name = bogoName,
                    Iterations = iterations,
                    Time = stopwatch.Elapsed.TotalMilliseconds.ToString("F3")
                });

                if (completed)
                {
                    visualizationSource = sorted;
                }
            }

            if (results.Count == 0)
            {
                MessageBox.Show("Выберите хотя бы одну сортировку");
                return;
            }

            DataGridResults.ItemsSource = results;

            if (visualizationSource == null)
            {
                visualizationSource = sourceNumbers;
            }

            List<double> visualizationNumbers = visualizationSource.Take(VisualizationLimit).ToList();
            TextBoxSortedArray.Text = FormatArray(visualizationNumbers);
        }

        private (List<double> sorted, int iterations) MeasureSort(string name, List<double> input, bool ascending,
            Func<List<double>, bool, (List<double> sorted, int iterations)> sortMethod,
            ref List<SortResult> results)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            var (sorted, iterations) = sortMethod(input, ascending);
            stopwatch.Stop();

            results.Add(new SortResult
            {
                Name = name,
                Iterations = iterations,
                Time = stopwatch.Elapsed.TotalMilliseconds.ToString("F3")
            });

            return (sorted, iterations);
        }

        private void ButtonGenerate_Click(object sender, RoutedEventArgs e)
        {
            if (RadioRandom.IsChecked == true)
            {
                GenerateRandom();
            }
            else if (RadioManual.IsChecked == true)
            {
                LoadManualInput();
            }
            else if (RadioFromFile.IsChecked == true)
            {
                LoadFromExcel();
            }
        }

        private void ButtonClear_Click(object sender, RoutedEventArgs e)
        {
            TextBoxInputArray.Text = string.Empty;
            TextBoxSortedArray.Text = string.Empty;
            DataGridResults.ItemsSource = null;
            DataGridManualInput.ItemsSource = new ObservableCollection<ManualNumber>();

            TextBoxA.Text = string.Empty;
            TextBoxB.Text = string.Empty;
            TextBoxN.Text = string.Empty;
            TextBoxBogoLimit.Text = DefaultBogoLimit.ToString();

            CheckBubble.IsChecked = false;
            CheckInsertion.IsChecked = false;
            CheckShaker.IsChecked = false;
            CheckQuick.IsChecked = false;
            CheckBogo.IsChecked = false;

            RadioAscending.IsChecked = true;
            RadioRandom.IsChecked = true;
        }

        private void ButtonExit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void LoadFromExcel()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Excel файлы (*.xlsx)|*.xlsx";
            openFileDialog.Title = "Выберите Excel файл";

            if (openFileDialog.ShowDialog() != true)
            {
                return;
            }

            List<double> fileNumbers = new List<double>();
            bool limitExceeded = false;

            try
            {
                using (var workbook = new XLWorkbook(openFileDialog.FileName))
                {
                    var worksheet = workbook.Worksheets.First();
                    var usedRange = worksheet.RangeUsed();

                    if (usedRange == null)
                    {
                        MessageBox.Show("Excel файл пуст");
                        return;
                    }

                    foreach (var row in usedRange.RowsUsed())
                    {
                        if (fileNumbers.Count >= MaxArraySize)
                        {
                            limitExceeded = true;
                            break;
                        }

                        foreach (var cell in row.CellsUsed())
                        {
                            if (fileNumbers.Count >= MaxArraySize)
                            {
                                limitExceeded = true;
                                break;
                            }

                            if (cell.DataType == XLDataType.Number)
                            {
                                double rawValue = cell.GetDouble();

                                if (double.IsNaN(rawValue) || double.IsInfinity(rawValue))
                                {
                                    continue;
                                }

                                fileNumbers.Add(Math.Round(rawValue, RoundingDigits));
                            }
                        }

                        if (limitExceeded)
                        {
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка чтения Excel: " + ex.Message);
                return;
            }

            if (limitExceeded)
            {
                MessageBox.Show("В Excel слишком много чисел (максимум " + MaxArraySize + ")");
                return;
            }

            if (fileNumbers.Count == 0)
            {
                MessageBox.Show("В Excel не найдено чисел");
                return;
            }

            TextBoxInputArray.Text = FormatArray(fileNumbers);
            ResetSortResults();
        }

        private void LoadManualInput()
        {
            if (DataGridManualInput.ItemsSource == null)
            {
                MessageBox.Show("Введите числа в таблицу ручного ввода");
                return;
            }

            List<double> manualNumbers = new List<double>();

            foreach (var item in DataGridManualInput.ItemsSource)
            {
                if (item is ManualNumber manualNumber)
                {
                    if (string.IsNullOrWhiteSpace(manualNumber.Value))
                    {
                        continue;
                    }

                    string cleanedValue = manualNumber.Value.Trim().Replace('.', ',');

                    if (!double.TryParse(cleanedValue, out double parsedValue))
                    {
                        MessageBox.Show("Некорректное число: " + manualNumber.Value);
                        return;
                    }

                    if (double.IsNaN(parsedValue) || double.IsInfinity(parsedValue))
                    {
                        MessageBox.Show("Число NaN или бесконечность недопустимо");
                        return;
                    }

                    manualNumbers.Add(Math.Round(parsedValue, RoundingDigits));
                }
            }

            if (manualNumbers.Count == 0)
            {
                MessageBox.Show("Введите хотя бы одно число");
                return;
            }

            if (manualNumbers.Count > MaxArraySize)
            {
                MessageBox.Show("Слишком много чисел (максимум " + MaxArraySize + ")");
                return;
            }

            TextBoxInputArray.Text = FormatArray(manualNumbers);
            ResetSortResults();
        }

        private void GenerateRandom()
        {
            if (string.IsNullOrWhiteSpace(TextBoxA.Text) ||
                string.IsNullOrWhiteSpace(TextBoxB.Text) ||
                string.IsNullOrWhiteSpace(TextBoxN.Text))
            {
                MessageBox.Show("Заполните все поля: a, b, n");
                return;
            }

            if (!double.TryParse(TextBoxA.Text, out double minimumValue) ||
                !double.TryParse(TextBoxB.Text, out double maximumValue) ||
                !int.TryParse(TextBoxN.Text, out int count))
            {
                MessageBox.Show("Введите корректные числа");
                return;
            }

            if (double.IsNaN(minimumValue) || double.IsInfinity(minimumValue) ||
                double.IsNaN(maximumValue) || double.IsInfinity(maximumValue))
            {
                MessageBox.Show("Числа a и b не могут быть NaN или бесконечностью");
                return;
            }

            if (count <= 0)
            {
                MessageBox.Show("n должно быть больше 0");
                return;
            }

            if (count > MaxArraySize)
            {
                MessageBox.Show("n не должно превышать " + MaxArraySize);
                return;
            }

            if (minimumValue > maximumValue)
            {
                MessageBox.Show("a не может быть больше b");
                return;
            }

            Random random = new Random();
            List<double> generatedNumbers = new List<double>();

            for (int index = 0; index < count; ++index)
            {
                double value = minimumValue + random.NextDouble() * (maximumValue - minimumValue);
                value = Math.Round(value, RoundingDigits);
                generatedNumbers.Add(value);
            }

            TextBoxInputArray.Text = FormatArray(generatedNumbers);
            ResetSortResults();
        }

        private (List<double> sorted, int iterations) BubbleSort(List<double> input, bool ascending)
        {
            List<double> result = new List<double>(input);
            int iterationCount = 0;

            for (int outerIndex = 0; outerIndex < result.Count - 1; ++outerIndex)
            {
                for (int innerIndex = 0; innerIndex < result.Count - outerIndex - 1; ++innerIndex)
                {
                    ++iterationCount;

                    if (NeedSwap(result[innerIndex], result[innerIndex + 1], ascending))
                    {
                        double temporary = result[innerIndex];
                        result[innerIndex] = result[innerIndex + 1];
                        result[innerIndex + 1] = temporary;
                    }
                }
            }

            return (result, iterationCount);
        }

        private (List<double> sorted, int iterations) InsertionSort(List<double> input, bool ascending)
        {
            List<double> result = new List<double>(input);
            int iterationCount = 0;

            for (int currentIndex = 1; currentIndex < result.Count; ++currentIndex)
            {
                double currentValue = result[currentIndex];
                int compareIndex = currentIndex - 1;

                while (compareIndex >= 0)
                {
                    ++iterationCount;

                    if (!NeedSwap(result[compareIndex], currentValue, ascending))
                    {
                        break;
                    }

                    result[compareIndex + 1] = result[compareIndex];
                    --compareIndex;
                }

                result[compareIndex + 1] = currentValue;
            }

            return (result, iterationCount);
        }

        private (List<double> sorted, int iterations) ShakerSort(List<double> input, bool ascending)
        {
            List<double> result = new List<double>(input);
            int iterationCount = 0;
            int leftBoundary = 0;
            int rightBoundary = result.Count - 1;

            while (leftBoundary < rightBoundary)
            {
                for (int forwardIndex = leftBoundary; forwardIndex < rightBoundary; ++forwardIndex)
                {
                    ++iterationCount;

                    if (NeedSwap(result[forwardIndex], result[forwardIndex + 1], ascending))
                    {
                        double temporary = result[forwardIndex];
                        result[forwardIndex] = result[forwardIndex + 1];
                        result[forwardIndex + 1] = temporary;
                    }
                }

                --rightBoundary;

                for (int backwardIndex = rightBoundary; backwardIndex > leftBoundary; --backwardIndex)
                {
                    ++iterationCount;

                    if (NeedSwap(result[backwardIndex - 1], result[backwardIndex], ascending))
                    {
                        double temporary = result[backwardIndex - 1];
                        result[backwardIndex - 1] = result[backwardIndex];
                        result[backwardIndex] = temporary;
                    }
                }

                ++leftBoundary;
            }

            return (result, iterationCount);
        }

        private (List<double> sorted, int iterations) QuickSort(List<double> input, bool ascending)
        {
            List<double> result = new List<double>(input);
            int iterationCount = 0;
            Random random = new Random();

            QuickSortRecursive(result, 0, result.Count - 1, ascending, ref iterationCount, random);

            return (result, iterationCount);
        }

        private void QuickSortRecursive(List<double> array, int leftIndex, int rightIndex, bool ascending, ref int iterationCount, Random random)
        {
            if (leftIndex >= rightIndex)
            {
                return;
            }

            int pivotIndex = leftIndex + random.Next(rightIndex - leftIndex + 1);
            double pivotValue = array[pivotIndex];

            int lessIndex = leftIndex;
            int currentIndex = leftIndex;
            int greaterIndex = rightIndex;

            while (currentIndex <= greaterIndex)
            {
                ++iterationCount;

                bool isLess = ascending ? array[currentIndex] < pivotValue : array[currentIndex] > pivotValue;
                bool isGreater = ascending ? array[currentIndex] > pivotValue : array[currentIndex] < pivotValue;

                if (isLess)
                {
                    Swap(array, lessIndex, currentIndex);
                    ++lessIndex;
                    ++currentIndex;
                }
                else if (isGreater)
                {
                    Swap(array, currentIndex, greaterIndex);
                    --greaterIndex;
                }
                else
                {
                    ++currentIndex;
                }
            }

            QuickSortRecursive(array, leftIndex, lessIndex - 1, ascending, ref iterationCount, random);
            QuickSortRecursive(array, greaterIndex + 1, rightIndex, ascending, ref iterationCount, random);
        }

        private void Swap(List<double> array, int firstIndex, int secondIndex)
        {
            if (firstIndex == secondIndex)
            {
                return;
            }

            double temporary = array[firstIndex];
            array[firstIndex] = array[secondIndex];
            array[secondIndex] = temporary;
        }

        private (List<double> sorted, int iterations, bool completed) BogoSort(List<double> input, bool ascending, int maxIterations)
        {
            List<double> result = new List<double>(input);
            int iterationCount = 0;
            Random random = new Random();

            while (iterationCount < maxIterations)
            {
                ++iterationCount;

                if (IsSorted(result, ascending))
                {
                    return (result, iterationCount, true);
                }

                ShuffleList(result, random);
            }

            return (result, iterationCount, false);
        }

        private bool IsSorted(List<double> array, bool ascending)
        {
            for (int index = 0; index < array.Count - 1; ++index)
            {
                if (ascending)
                {
                    if (array[index] > array[index + 1])
                    {
                        return false;
                    }
                }
                else
                {
                    if (array[index] < array[index + 1])
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private void ShuffleList(List<double> array, Random random)
        {
            for (int index = array.Count - 1; index > 0; --index)
            {
                int randomIndex = random.Next(index + 1);

                double temporary = array[index];
                array[index] = array[randomIndex];
                array[randomIndex] = temporary;
            }
        }
    }
}