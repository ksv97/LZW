using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Threading;
using System.Windows;
using System.ComponentModel;
using System.Windows.Controls;
using System.Diagnostics;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Timers = System.Timers;
using Microsoft.Win32;
using System.IO;

namespace Кодер_LZW
{

    //ВАРИАНТ ОПТИМИЗАЦИИ - MemoryStream и fileBytes (см. ВК)
    //

    delegate void UpdateProgressBarDelegate(DependencyProperty dp, object value);

    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static event Action BufferBytesChanged;
        public static event Action InputStreamEnded;

        FileInfo inputFile;
        BackgroundWorker worker;

        byte maxLengthOfCode;
        byte bufferSize;  // размер буфера в байтах      
        byte[] lastCodeOfChain; // ничто иное как результат логической инкрементации последнего кода в таблице
        double progress = 0; // количество байтов, прочитанное в кодируемом файле
        int countOfBitsEncoded; // подсчет количества бит при кодировании        
        int timeOfEncoding;
        long fileLength; // длина файла
        byte[] fileBytes; // массив байтов из файла (для оптимизации работы)
        string filePath = "";
        Dictionary<List<byte>, byte[]> codeTable;        

        public MainWindow()
        {
            InitializeComponent();
            inputTxtBox.Text = "";
            outputTxtBlock.Text = "";
            byte.TryParse(txtbxBufSize.Text, out bufferSize);
            byte.TryParse(txtbxCodeLen.Text, out maxLengthOfCode);
            worker = new BackgroundWorker();
            worker.DoWork += Worker_DoWork;

            worker.RunWorkerCompleted += Worker_RunWorkerCompleted;
        }

        public void Encode ()
        {
            UpdateProgressBarDelegate updProgress = new UpdateProgressBarDelegate(pgBarEncoding.SetValue); // создать объект делегата обновления прогресс-бара                       
            Stopwatch stopwatch = new Stopwatch();            
            Bufer buffer = new Bufer(bufferSize, filePath);
            int countOfBytesForOnePercent = (int)fileLength / 100;
            double percentProgress = 0;

            stopwatch.Start();
            InitialiseCodeTable(); // Сформировать корневую часть таблицы цепочек
            List<byte> prefix = new List<byte>(); // префикс = пустая строка

            foreach (byte currentByte in fileBytes)
            {
                // процедура создания цепочки prefix + символ
                List<byte> prefixWithCurrentSymbol = new List<byte>();
                foreach (byte b1 in prefix)
                    prefixWithCurrentSymbol.Add(b1);
                prefixWithCurrentSymbol.Add(currentByte);

                if (codeTable.ContainsKey(prefixWithCurrentSymbol) == true) // если цепочка prefix + текущий символ присутствует в таблице цепочек
                {
                    prefix.Add(currentByte); // prefix = prefix + символ
                }
                else
                {
                    byte[] outputCode = codeTable[prefix];
                    OutputCodeOfChain(outputCode, buffer); // вывести код префикса в выходной поток, используя буфер

                    // Исключить переполнение таблицы цепочек. Удалить динамическую часть таблицы при достижении максимума
                    if (codeTable.Count == maxLengthOfCode * 256)
                    {
                        // удаление всех записей длиной больше двух
                        for (int i = 256; i < codeTable.Keys.Count; i++)
                        {
                            List<byte> key = codeTable.Keys.ElementAt(i);
                            codeTable.Remove(key);
                        }
                        lastCodeOfChain = new byte[] { 1, 0, 0, 0, 0, 0, 0, 0, 0 };
                    }

                    List<byte> newChain = new List<byte>();
                    newChain = prefixWithCurrentSymbol.GetRange(0, prefixWithCurrentSymbol.Count); // кодируемая цепочка равна цепочке префикс + символ
                    byte[] codeOfChain = new byte[lastCodeOfChain.Length]; // длина создаваемого массива равна длине последнего кода (т.к. он уже инкрементирован под нужное значение)
                    lastCodeOfChain.CopyTo(codeOfChain, 0); // новый код равен логической инкрементации предыдущего кода в таблице (т.е. lastCodeOfChain)

                    codeTable.Add(newChain, codeOfChain); // Добавить код для цепочки "префикс + символ" в таблицу кодов цепочек

                    countOfBitsEncoded += outputCode.Length; // инкрементировать количество бит
                    lastCodeOfChain = LogicIncementation(lastCodeOfChain); // сразу инкрементировать последний код

                    // префикс = символ
                    prefix.Clear();
                    prefix.Add(currentByte);

                }

                if (progress == fileLength - 1) // если последний символ
                {

                    // найти код в таблице для цепочки последнего байта и вывести в выходной поток с использованием буфера
                    OutputCodeOfChain(codeTable[new List<byte>() { currentByte }], buffer);
                    countOfBitsEncoded += 8;
                }

                progress++;
                if (progress % countOfBytesForOnePercent == 0)                
                    Dispatcher.Invoke(updProgress, new object[] { ProgressBar.ValueProperty, ++percentProgress }); // обновление прогресс-бара                    
                
            }
            try
            {
                InputStreamEnded?.Invoke();
            }
            catch (ApplicationException ex)
            {
                MessageBox.Show(ex.Message);
            }
            stopwatch.Stop();
            timeOfEncoding = (int)stopwatch.ElapsedMilliseconds / 1000 + 1; ;
         
        }

        /// <summary>
        /// Версия аутпута, работающая с буфером. Пока находится в разработке 
        /// </summary>
        /// <param name="codeOfChain"></param>
        /// <param name="buffer"></param>
        public void OutputCodeOfChain(byte[] codeOfChain, Bufer buffer)
        {
            foreach (byte b in codeOfChain)
            {
                buffer.Bits.Add(b);
                BufferBytesChanged?.Invoke();             
            }
        }

        public void InitialiseCodeTable ()
        {
            codeTable = new Dictionary<List<byte>, byte[]>(256,new ListEqualityComparer());
            lastCodeOfChain = new byte[8] { 0, 0, 0, 0, 0, 0, 0, 0 }; // инициализация кода первого байта = все нули
            for (byte i = 0; i <= byte.MaxValue; i++)
            {
                List<byte> chain = new List<byte>() { i };                
                byte[] codeOfChain = new byte[8];
                lastCodeOfChain.CopyTo(codeOfChain, 0); // копирование битов последнего кода к текущему коду
                codeTable.Add(chain, codeOfChain); // добавление нового кода в таблицу кодов
                lastCodeOfChain = LogicIncementation(lastCodeOfChain);
                if (i == byte.MaxValue) break;
            }
        }

        byte[] LogicIncementation (byte[] array)
        {
            int i = array.Length - 1;
            if (array[i] == 0)
                array[i] = 1;
            else
            {
                // проверка байтов на полноту
                bool isFull = true;
                for (int j = 0; j < array.Length && isFull == true; j++)
                {
                    if (array[j] == 0)
                        isFull = false;
                }

                // если полученный массив битов состоит не из одних единиц, инкрементировать его без увеличения разрядности
                if (!isFull)
                {
                    // обнулять биты до тех пор, пока не найдем "свободный ноль"
                    do
                    {
                        array[i] = 0;
                        i--;
                    } while (array[i] != 0);
                    array[i] = 1; // заполнить "свободный ноль" единицей
                }
                else // иначе увеличить разрядность последовательности бит до кода максимальной длины
                {
                    array = new byte[array.Length + 1];
                    for (int j = 0; j < array.Length; j++)
                    {
                        if (j == 0)
                            array[j] = 1;
                        else array[j] = 0;
                    }
                }
            }
            return array;
        }

        private void chooseFileBtn_Click(object sender, RoutedEventArgs e)
        {


            OpenFileDialog openFileDialog = new OpenFileDialog()
            {
                Multiselect = false,
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop), // установка позиции выбора файла на адрес рабочего стола
                Title = "Выберите файл для кодирования"                
            };
            if (openFileDialog.ShowDialog() == true)
            {
                inputTxtBox.Text = "";
                outputTxtBlock.Text = "";
                try
                {
                    inputFile = new FileInfo(openFileDialog.FileName);
                    fileLength = inputFile.Length;
                    fileBytes = File.ReadAllBytes(openFileDialog.FileName);
                    inputTxtBox.Text = "Кодируемый файл: " + openFileDialog.FileName;
                    inputTxtBox.Text += Environment.NewLine + "Длина исходного файла = " + fileLength + " байт = " + fileLength / 1024 + " кБайт";                    
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error!" + ex.Message);
                }
            }
        }

        private void encodeBtn_Click(object sender, RoutedEventArgs e)
        {
            if (inputFile != null)
            {
                SaveFileDialog saveFileDialog = new SaveFileDialog()
                {
                    OverwritePrompt = true,
                    InitialDirectory = inputFile.DirectoryName,
                    Title = "Выберите расположение закодированного файла",
                    Filter = "Файл LZW (*.lzw)|*.lzw"
                };
                
                if (saveFileDialog.ShowDialog() == true)
                {
                    filePath = saveFileDialog.FileName;
                    if (File.Exists(saveFileDialog.FileName))
                        try
                        {
                            File.Delete(filePath);
                        }
                        catch (FileNotFoundException ex)
                        {
                            MessageBox.Show(ex.Message);
                        }                    
                    countOfBitsEncoded = 0;
                    //pgBarEncoding.Maximum = inputFile.Length;
                    pgBarEncoding.Value = 0;
                    outputTxtBlock.Text = "";

                    worker.RunWorkerAsync();
                }
                chooseFileBtn.IsEnabled = false;
                encodeBtn.IsEnabled = false;                
                txtbxBufSize.IsReadOnly = true;
                txtbxCodeLen.IsReadOnly = true;
            }
            else
            {
                MessageBox.Show("Не выбран файл для кодирования! Пожалуйста, выберите файл, затем нажмите на кнопку \"Кодировать\"","Файл не выбран",MessageBoxButton.OK,MessageBoxImage.Error);
            }

        }


        private void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            Encode();
        }

        private void Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            chooseFileBtn.IsEnabled = true;
            encodeBtn.IsEnabled = true;
            txtbxBufSize.IsReadOnly = false;
            txtbxCodeLen.IsReadOnly = false;
            outputTxtBlock.Text = "Закодированный файл: " + filePath;
            outputTxtBlock.Text += Environment.NewLine + "Длина закодированного файла = " + countOfBitsEncoded / 8 + " байт = " + countOfBitsEncoded / (8 * 1024) + "Кбайт";
            outputTxtBlock.Text += Environment.NewLine + "Время кодирования - " + timeOfEncoding;            
        }

        private void txtbxBufSize_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (byte.TryParse(txtbxBufSize.Text, out bufferSize) == false)
                bufferSize = 6;// dfault     
        }

        private void txtbxCodeLen_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (byte.TryParse(txtbxCodeLen.Text, out maxLengthOfCode) == false)
                maxLengthOfCode = 12;// dfault
        }
    }
}
