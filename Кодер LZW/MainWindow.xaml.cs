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
using Microsoft.Win32;
using System.IO;

namespace Кодер_LZW
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public struct Code
        {
            public List<byte> Chain;
            public byte[] CodeOfChain;
        }

        int lengthOfCode = 12; // Величина максимального кода.
        byte[] lastCodeOfChain; // ничто иное как результат логической инкрементации последнего кода в таблице
        byte[] fileBytes; // байты, считанные из файла
        List<Code> codeTable;

        public MainWindow()
        {
            InitializeComponent();
            inputTxtBox.Text = "";
            outputTxtBlock.Text = "";
            codeTableTxtBlock.Text = "";
            //InitialiseCodeTable();
            //PrintCodeTable();
        }

        public void Encode ()
        {            
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
                
                if (SearchChainInCodeTable(prefixWithCurrentSymbol) == true) // если цепочка prefix + текущий символ присутствует в таблице цепочек
                {
                    prefix.Add(currentByte); // prefix = prefix + символ
                }
                else
                {
                    byte[] outputCode = FindCodeOfChain(prefix);
                    OutputCodeOfChain(outputCode); // вывести код префикса в выходной поток
                    Code code = new Code(); // создать новый код
                    code.Chain = new List<byte>();
                    code.Chain = prefixWithCurrentSymbol.GetRange(0, prefixWithCurrentSymbol.Count); // кодируемая цепочка равна цепочке префикс + символ
                    code.CodeOfChain = new byte[lastCodeOfChain.Length]; // длина создаваемого массива равна длине последнего кода (т.к. он уже инкрементирован под нужное значение)
                    lastCodeOfChain.CopyTo(code.CodeOfChain, 0); // новый код равен логической инкрементации предыдущего кода в таблице (т.е. lastCodeOfChain)
                    codeTable.Add(code); // Добавить код для цепочки "префикс + символ" в таблицу кодов цепочек

                    countOfBitsEncoded += outputCode.Length; // инкрементировать количество бит
                    lastCodeOfChain = LogicIncementation(lastCodeOfChain); // сразу инкрементировать последний код
                    
                    // префикс = символ
                    prefix.Clear();
                    prefix.Add(currentByte);

                }

                if (currentByte == fileBytes.ElementAt(fileBytes.Length - 1)) // если последний символ
                {
                    
                    OutputCodeOfChain(FindCodeOfChain(new List<byte>() { currentByte })); // найти код в таблице для цепочки последнего байта и вывести в выходной поток
                    countOfBitsEncoded += 8;
                }
            }
            outputTxtBlock.Text += Environment.NewLine + "Длина закодированного файла - " + countOfBitsEncoded / 8 + " байт = " + countOfBitsEncoded + " бит";
        }

        public void OutputCodeOfChain (byte[] codeOfChain)
        {
            //int countOfSymbolsForSpacing = codeOfChain.Length;
            foreach (byte b in codeOfChain)
            {
                outputTxtBlock.Text += b;
                //countOfSymbolsForSpacing--;
                //if (countOfSymbolsForSpacing % 8 == 0 && countOfSymbolsForSpacing != 0)
                //    outputTxtBlock.Text += " ";
            }
            outputTxtBlock.Text += " ";
        }

        public byte[] FindCodeOfChain (List<byte> chain)
        {
            foreach (Code currentCode in codeTable)
            {
                // имеет смысл проверять только те цепочки в таблице, у которых такая же длина
                if (currentCode.Chain.Count == chain.Count)
                {
                    bool isFound = true;

                    // сопоставляем байты цепочки в таблице с входной цепочкой
                    for (int i = 0; i < currentCode.Chain.Count; i++)
                    {
                        if (currentCode.Chain.ElementAt(i) != chain.ElementAt(i))  // если нашелся хоть один конфликт, эта цепочка таблицы нас не удовлетворяет
                            isFound = false;
                    }
                    if (isFound) // если не нашлось противоречий, вернуть код для найденной цепочки
                    {
                        return currentCode.CodeOfChain;
                    }
                }
            }
            return null; // после проверки всей тааблицы вернуть false (не нашли ничего)
        }

        public bool SearchChainInCodeTable (List<byte> chain)
        {
            
            foreach (Code currentCode in codeTable)
            {
                // имеет смысл проверять только те цепочки в таблице, у которых такая же длина
                if (currentCode.Chain.Count == chain.Count)
                {
                    bool isFound = true;

                    // сопоставляем байты цепочки в таблице с входной цепочкой
                    for (int i = 0; i < currentCode.Chain.Count; i++)
                    {
                        if (currentCode.Chain.ElementAt(i) != chain.ElementAt(i))  // если нашелся хоть один конфликт, эта цепочка таблицы нас не удовлетворяет
                            isFound = false;
                    }
                    if (isFound) // если не нашлось противоречий, вернуть true
                        return true;
                }
            }
            return false; // после проверки всей тааблицы вернуть false (не нашли ничего)
        }


        public void InitialiseCodeTable ()
        {
            codeTable = new List<Code>();
            lastCodeOfChain = new byte[8] { 0, 0, 0, 0, 0, 0, 0, 0 }; // инициализация кода первого байта = все нули
            for (byte i = 0; i <= byte.MaxValue; i++)
            {
                Code newCode = new Code();
                newCode.Chain = new List<byte>() { i }; // инициализация новой цепочки кода значением текущего байта в цикле
                newCode.CodeOfChain = new byte[8];
                lastCodeOfChain.CopyTo(newCode.CodeOfChain, 0); // копирование битов последнего кода к текущему коду
                codeTable.Add(newCode); // добавление нового кода в таблицу кодов
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

                    //array = new byte[lengthOfCode];
                    //for (int j = 0; j < array.Length; j++)
                    //{
                    //    if (j == (lengthOfCode - 8) - 1) // заполняем 9 бит (j = 12 - 9 = 3 при коде типа 0000 0000 0000
                    //        array[j] = 1;
                    //    else array[j] = 0;
                    //}
                }
            }
            return array;
        }

        public void PrintCodeTable ()
        {
            foreach (Code code in codeTable)
            {
                foreach (byte byteOfChain in code.Chain)
                {
                    codeTableTxtBlock.Text += byteOfChain + " ";
                }
                codeTableTxtBlock.Text += " = ";
                int counterForSpacing = code.CodeOfChain.Length;
                foreach (byte bit in code.CodeOfChain)
                {
                    codeTableTxtBlock.Text += bit;
                    counterForSpacing--;
                    if (counterForSpacing % 8 == 0 && counterForSpacing != 0)
                        codeTableTxtBlock.Text += " ";
                }
                codeTableTxtBlock.Text += Environment.NewLine;
            }
        }

        private void chooseFileBtn_Click(object sender, RoutedEventArgs e)
        {            
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Multiselect = false;
            openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop); // установка позиции выбора файла на адрес рабочего стола
            
            if (openFileDialog.ShowDialog() == true)
            {
                //FileStream myStream;
                try
                {
                    //myStream = new FileStream(openFileDialog.FileName, FileMode.Open, FileAccess.Read);
                    fileBytes = File.ReadAllBytes(openFileDialog.FileName);
                    inputTxtBox.Text = "Поток байтов из файла: " + Environment.NewLine;
                    int bitCounter = 0;
                    foreach (byte b in fileBytes)
                    {
                        inputTxtBox.Text += b + " ";
                        bitCounter += 8;
                    }
                    inputTxtBox.Text += Environment.NewLine + "Длина исходного файла - " + fileBytes.Length + " байт = " + bitCounter + " бит";
                    
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
