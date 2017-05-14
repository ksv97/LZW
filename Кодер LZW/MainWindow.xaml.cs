using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Timers;
using Microsoft.Win32;
using System.IO;

namespace Кодер_LZW
{
    // BUG: не выводит output log в файл

    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static event Action BufferBytesChanged;
        public static event Action InputStreamEnded;
       
        const int maxLengthOfCode = 11;
        const byte bufferSize = 6;  // размер буфера в байтах      
        byte[] lastCodeOfChain; // ничто иное как результат логической инкрементации последнего кода в таблице
        byte[] fileBytes; // байты, считанные из файла 
        int timeOfEncoding;
        Dictionary<List<byte>, byte[]> codeTable;                 

        public MainWindow()
        {
            InitializeComponent();
            inputTxtBox.Text = "";
            outputTxtBlock.Text = "";
            codeTableTxtBlock.Text = "";

        }

        public void Encode ()
        {
            string filePath = "outputLog.txt";
            Bufer buffer = new Bufer(bufferSize, filePath);
            buffer.SendByteToOutput += Buffer_SendByteToOutput;

            InitialiseCodeTable(); // Сформировать корневую часть таблицы цепочек
            List<byte> prefix = new List<byte>(); // префикс = пустая строка
            int countOfBitsEncoded = 0; // подсчет количества бит при кодировании
            foreach (byte currentByte in fileBytes) // пока не конец входного потока, выполнять
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
                        lastCodeOfChain = new byte[] { 1, 0, 0, 0, 0, 0, 0, 0, 0};
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

                if (currentByte == fileBytes.ElementAt(fileBytes.Length - 1)) // если последний символ
                {
                    
                    // найти код в таблице для цепочки последнего байта и вывести в выходной поток с использованием буфера
                    OutputCodeOfChain(codeTable[new List<byte>() { currentByte }], buffer);
                    countOfBitsEncoded += 8;
                }
            }
            try
            {
                InputStreamEnded?.Invoke();
            }
            catch (ApplicationException ex)
            {
                MessageBox.Show(ex.Message);
            }            
            outputTxtBlock.Text = "Длина закодированного файла - " + countOfBitsEncoded / 8 + " байт = " + countOfBitsEncoded + " бит";
            outputTxtBlock.Text += Environment.NewLine + "Время кодирования - " + timeOfEncoding;
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            timeOfEncoding++;
        }

        private void Buffer_SendByteToOutput(byte currBit, int bitOfCurrentByte)
        {
            outputTxtBlock.Text += currBit;
            if (bitOfCurrentByte == 7) outputTxtBlock.Text += "|";
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

        public void PrintCodeTable()
        {
            for (int i = 0; i < codeTable.Count; i++)
            {
                foreach (byte byteOfChain in codeTable.ElementAt(i).Key)
                {
                    codeTableTxtBlock.Text += byteOfChain + " ";
                }
                codeTableTxtBlock.Text += " = ";                
                foreach (byte bit in codeTable.ElementAt(i).Value)
                {
                    codeTableTxtBlock.Text += bit;                    
                }
                codeTableTxtBlock.Text += Environment.NewLine;
            }
        }

        private void chooseFileBtn_Click(object sender, RoutedEventArgs e)
        {
            codeTableTxtBlock.Text = "";
            inputTxtBox.Text = "";
            outputTxtBlock.Text = "";

            OpenFileDialog openFileDialog = new OpenFileDialog()
            {
                Multiselect = false,
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) // установка позиции выбора файла на адрес рабочего стола
            };
            if (openFileDialog.ShowDialog() == true)
            {                
                try
                {                    
                    fileBytes = File.ReadAllBytes(openFileDialog.FileName);
                    try
                    {
                        File.Delete("outputLog.txt");
                    }
                    catch (FileNotFoundException ex)
                    {
                        MessageBox.Show(ex.Message + " at " + ex.Source);
                    }
                    
                    //inputTxtBox.Text = "Поток байтов из файла: " + Environment.NewLine;

                    using (BinaryWriter bWriter = new BinaryWriter(File.Create("inputLog.txt")))
                    {                        
                        
                        int bitCounter = 0;
                        foreach (byte b in fileBytes)
                        {
                            bWriter.Write(b);
                            //inputTxtBox.Text += b + " ";
                            bitCounter += 8;
                        }
                        //sWriter.WriteLine();
                        //sWriter.WriteLine("Длина исходного файла - " + fileBytes.Length + " байт = " + bitCounter + " бит");
                        inputTxtBox.Text = "Длина исходного файла - " + (fileBytes.Length / 1024).ToString() + "Кбайт = " + fileBytes.Length + " байт = " + bitCounter + " бит";
                    }

                    
                    
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error!" + ex.Message);
                }
            }
        }

        private void encodeBtn_Click(object sender, RoutedEventArgs e)
        {
            outputTxtBlock.Text = "";
            Encode();
            PrintCodeTable();
        }
    }
}
