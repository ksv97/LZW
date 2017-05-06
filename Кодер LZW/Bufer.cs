using System;
using System.Collections.Generic;
using System.IO;

namespace Кодер_LZW
{
    public class Bufer
    {
        /// <summary>
        /// Событие, отправляющее байт в выходной поток
        /// </summary>
        public event Action<byte, int> SendByteToOutput;

        /// <summary>
        /// Размер буфера в байтах
        /// </summary>
        private readonly int size;

        /// <summary>
        /// Поток вывода данных в файл
        /// </summary>
        private StreamWriter sWriter = File.AppendText("outputLog.txt");

        public List<byte> Bits { private set; get; }
        public int CurrentByte { private set; get; }
        public int CurrentBit { private set; get; }
        
        public Bufer (byte size)
        {
            Bits = new List<byte>();
            CurrentByte = 0;
            CurrentBit = 0;
            this.size = size;
            MainWindow.BufferBytesChanged += MainWindow_BufferBytesChanged;
            MainWindow.InputStreamEnded += MainWindow_InputStreamEnded; ;
        }

        private void MainWindow_InputStreamEnded()
        {            
            byte countOfFilledBytes = (byte)(Bits.Count / 8); 
            // найти позицию, с которой начинается незаконченный байт (остаток от деления на 8?)
            // определить, сколько бит заполнено в незаполненном байте, и запомнить это число
            // дописать в конец фиктивные нули
            // вывести буфер в выходной поток
            // добавить в конец выходного потока байт, который задан switch'ем
            sWriter.Close();
        }

        private void MainWindow_BufferBytesChanged()
        {
            // При получении каждого нового бита буфер перемещает свой указатель бита. Если он переходит за пределы байта, то указатель обнуляется, а байт инкрементируется
            CurrentBit += 1;
            if (CurrentBit == 8)
            {
                CurrentBit = 0;
                CurrentByte++;
            }           
            if (Bits.Count == size * 8)
            {
                Output();
                Bits.Clear();
            }
                
        }

        public void Output()
        {
            CurrentBit = 0;
            CurrentByte = 0;
            foreach (byte b in Bits)
            {
                //SendByteToOutput?.Invoke(b, CurrentBit);
                CurrentByte += b * (int)Math.Pow(2, CurrentBit);
                if (++CurrentBit == 8)
                {
                    sWriter.Write(CurrentByte);
                    sWriter.Write(" ");
                    CurrentBit = 0;
                    CurrentByte = 0;
                }
            }
        }

    }
}
