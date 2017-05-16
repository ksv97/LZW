﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Text;

namespace Кодер_LZW
{
    public class Bufer
    {
        /// <summary>
        /// Размер буфера в байтах
        /// </summary>
        private readonly int size;

        /// <summary>
        /// Поток вывода данных в файл
        /// </summary>
        private BinaryWriter bWriter;

        public List<byte> Bits { private set; get; }
        public byte CurrentByte { private set; get; }
        public byte CurrentBit { private set; get; }
        private string filePath;

        public Bufer (byte size, string filePath)
        {
            this.filePath = filePath;
            bWriter = new BinaryWriter(File.OpenWrite(filePath));            

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
                //MessageBox.Show(this.GetHashCode().ToString() + " in InputStreamEnded");
            }

            // КОСТЫЛЬ!!!


            Output(); // вывести буфер в выходной поток
            MainWindow.BufferBytesChanged -= MainWindow_BufferBytesChanged;
            MainWindow.BufferBytesChanged -= MainWindow_InputStreamEnded;
            bWriter.Close();
            bWriter.Dispose();
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
                CurrentByte += (byte)(b * (int)Math.Pow(2, CurrentBit));
                if (++CurrentBit == 8)
                {
                    // MessageBox.Show(this.GetHashCode().ToString() + " in Output");
                    try
                    {
                        bWriter.Write(CurrentByte);
                    }
                    catch (ObjectDisposedException ex)
                    {
                        this.bWriter = new BinaryWriter(File.OpenWrite(filePath));
                        bWriter.Seek(1, SeekOrigin.End);
                    }                    
                    CurrentBit = 0;
                    CurrentByte = 0;
                }
            }
        }

    }
}
