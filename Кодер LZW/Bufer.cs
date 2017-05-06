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
            byte countOfFilledBitsInLastByte = (byte)(Bits.Count % 8); // найти количество бит в незаполненном байте
            if (countOfFilledBitsInLastByte != 0) // если последний байт не заполнен
            {
                // дописать в конец фиктивные нули
                for (CurrentBit = 0; CurrentBit < 8 - countOfFilledBitsInLastByte; CurrentBit++)
                {
                    Bits.Add(0);
                }
                byte[] lastByteMask;
                switch (countOfFilledBitsInLastByte) // сгенерировать маску последнего байта
                {
                    case 1: lastByteMask = new byte[8] { 1, 0, 0, 0, 0, 0, 0, 0 }; break;
                    case 2: lastByteMask = new byte[8] { 0, 1, 0, 0, 0, 0, 0, 0 }; break;
                    case 3: lastByteMask = new byte[8] { 0, 0, 1, 0, 0, 0, 0, 0 }; break;
                    case 4: lastByteMask = new byte[8] { 0, 0, 0, 1, 0, 0, 0, 0 }; break;
                    case 5: lastByteMask = new byte[8] { 0, 0, 0, 0, 1, 0, 0, 0 }; break;
                    case 6: lastByteMask = new byte[8] { 0, 0, 0, 0, 0, 1, 0, 0 }; break;
                    case 7: lastByteMask = new byte[8] { 0, 0, 0, 0, 0, 0, 1, 0 }; break;
                    default: throw new ApplicationException("Bad Count Of Filled Bits In Last Byte");
                }
                Bits.AddRange(lastByteMask);

            }
            Output(); // вывести буфер в выходной поток
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
