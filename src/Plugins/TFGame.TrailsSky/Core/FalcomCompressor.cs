﻿using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using TF.IO;

namespace TFGame.TrailsSky
{
    // Código adaptado de https://heroesoflegend.org/forums/viewtopic.php?f=38&t=289

    public class FalcomCompressor
    {
        private enum FlagType
        {
            Literal,
            ShortJump,
            LongJump
        }

        private const int CHUNK_SIZE_UNC_MAX = 0xFFF0;

        private class CompressedBinaryReader : ExtendedBinaryReader
        {
            private ushort _currentFlagValue;
            private byte _remainingFlagCount;

            public CompressedBinaryReader(Stream input, Endianness endianness = Endianness.LittleEndian) : base(input, endianness)
            {
            }

            public void InitializeFlags()
            {
                _currentFlagValue = (ushort)(ReadUInt16() >> 8);
                _remainingFlagCount = 0x08;
            }

            public FlagType ReadFlagType()
            {
                var isFlagSet = ReadFlag();

                if (!isFlagSet)
                {
                    return FlagType.Literal;
                }

                isFlagSet = ReadFlag();
                return isFlagSet ? FlagType.LongJump : FlagType.ShortJump;
            }


            public bool ReadFlag()
            {
                if (_remainingFlagCount == 0x00)
                {
                    var data = ReadUInt16();
                    _currentFlagValue = data;
                    _remainingFlagCount = 0x10;
                }

                var result = (_currentFlagValue & 0x01) == 0x01;

                _currentFlagValue >>= 0x01;
                _remainingFlagCount--;

                return result;
            }

            public int ReadFlagValue(int numBits)
            {
                var value = 0;

                for (var i = 0; i < numBits; i++)
                {
                    value <<= 0x01;

                    var isFlagSet = ReadFlag();
                    value |= isFlagSet ? 0x01 : 0x00;
                }

                return value;
            }

            public int ReadCopyCount()
            {
                if (ReadFlag())
                {
                    return 0x02;
                }

                if (ReadFlag())
                {
                    return 0x03;
                }

                if (ReadFlag())
                {
                    return 0x04;
                }

                if (ReadFlag())
                {
                    return 0x05;
                }

                if (ReadFlag())
                {
                    return ReadFlagValue(3) + 0x06;
                }

                return ReadByte() + 0x0E;
            }
        }

        private class CompressedBinaryWriter : ExtendedBinaryWriter
        {
            private ushort _currentFlagValue;
            private byte _remainingFlagCount;
            private MemoryStream _outputBuffer;

            public CompressedBinaryWriter(Stream input, Endianness endianness = Endianness.LittleEndian) : base(input, endianness)
            {
                _outputBuffer = new MemoryStream();
            }

            public void InitializeFlags()
            {
                _currentFlagValue = 0;
                _remainingFlagCount = 0x08;
            }

            public void WriteFlag(bool isSet)
            {
                if (isSet)
                {
                    _currentFlagValue |= 0x8000;
                }

                _remainingFlagCount--;

                if (_remainingFlagCount == 0)
                {
                    base.Write(_currentFlagValue);
                    base.Write(_outputBuffer.ToArray());
                    _outputBuffer.Close();
                    _outputBuffer.Dispose();
                    _outputBuffer = new MemoryStream();

                    _remainingFlagCount = 0x10;
                    _currentFlagValue = 0x0000;
                }
                else
                {
                    _currentFlagValue >>= 0x01;
                }
            }

            public void WriteFlagValue(int value, int numBits)
            {
                for (var i = numBits - 1; i >= 0; i--)
                {
                    var flagValue = ((value >> i) & 0x01) == 0x01;
                    WriteFlag(flagValue);
                }
            }

            public void WriteCopyCount(int copyCount)
            {
                var i = 2;
                var limit = Math.Min(5, copyCount);
                while (i < limit)
                {
                    WriteFlag(false);
                    i++;
                }

                if (copyCount >= 0x06)
                {
                    WriteFlag(false);
                    if (copyCount >= 0x0E)
                    {
                        copyCount -= 0x0E;
                        Write((byte)copyCount);
                        WriteFlag(false);
                    }
                    else
                    {
                        WriteFlag(true);
                        copyCount -= 0x06;
                        WriteFlagValue(copyCount, 3);
                    }
                }
                else
                {
                    WriteFlag(true);
                }
            }

            protected override void Dispose(bool disposing)
            {
                if (_remainingFlagCount != 0x10)
                {
                    var count = _remainingFlagCount;
                    for (var i = 0; i < count; i++)
                    {
                        WriteFlag(false);
                    }
                }

                _outputBuffer.Close();
                _outputBuffer.Dispose();

                base.Dispose(disposing);
            }

            public override void Write(byte value)
            {
                _outputBuffer.WriteByte(value);
            }
        }

        public static byte[] Decompress(byte[] compressedData)
        {
            using (var input = new CompressedBinaryReader(new MemoryStream(compressedData)))
            using (var output = new MemoryStream())
            {
                byte endByte;
                do
                {
                    var chunkSize = input.ReadUInt16();
                    var data = DecompressChunk(input, chunkSize);
                    output.Write(data, 0, data.Length);

                    endByte = input.ReadByte();

                } while (endByte != 0);

                return output.ToArray();
            }
        }

        private static byte[] DecompressChunk(CompressedBinaryReader input, ushort chunkSize)
        { 
            using (var output = new MemoryStream())
            {
                var startPos = input.Position;

                input.InitializeFlags();

                var finished = false;

                while (!finished)
                {
                    if (input.Position >= startPos + chunkSize)
                    {
                        throw new Exception(); // El fichero está corrupto
                    }

                    var type = input.ReadFlagType();

                    switch (type)
                    {
                        case FlagType.Literal:
                        {
                            output.WriteByte(input.ReadByte());
                        }
                        break;

                        case FlagType.ShortJump:
                        {
                            var copyDistance = (int) input.ReadByte();
                            var copyCount = input.ReadCopyCount();

                            //Debug.WriteLine($"{input.Position:X4}\tMatch\t{copyCount}\t{copyDistance}");

                            CopyBytes(output, copyDistance, copyCount);
                        }
                        break;

                        case FlagType.LongJump:
                        {
                            var highDistance = input.ReadFlagValue(5);
                            var lowDistance = input.ReadByte();

                            if (highDistance != 0 || lowDistance > 2)
                            {
                                var copyDistance = (highDistance << 8) | lowDistance;
                                var copyCount = input.ReadCopyCount();

                                //Debug.WriteLine($"{input.Position:X4}\tMatch\t{copyCount}\t{copyDistance}");

                                CopyBytes(output, copyDistance, copyCount);
                            }
                            else if (lowDistance == 0)
                            {
                                finished = true;
                            }
                            else
                            {
                                var isBranch = input.ReadFlag();

                                var count = input.ReadFlagValue(4);

                                if (isBranch)
                                {
                                    count <<= 8;
                                    count |= input.ReadByte();
                                }

                                count += 0x0E;

                                //Debug.WriteLine($"{input.Position:X4}\tRepeat\t{count}");

                                var b = input.ReadByte();
                                var data = Enumerable.Repeat(b, count).ToArray();
                                output.Write(data, 0, count);
                            }
                        }
                        break;

                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
                return output.ToArray();
            }
        }

        private static void CopyBytes(Stream s, int distance, int count)
        {
            for (var i = 0; i < count; i++)
            {
                s.Seek(-distance, SeekOrigin.End);
                var data = (byte)s.ReadByte();
                s.Seek(0, SeekOrigin.End);

                s.WriteByte(data);
            }
        }

        public static byte[] Compress(byte[] uncompressedData)
        {
            using (var outputMemoryStream = new MemoryStream())
            {
                using (var output = new ExtendedBinaryWriter(outputMemoryStream))
                {
                    var pos = 0;
                    var chunkCount = (int)(uncompressedData.Length / (double)CHUNK_SIZE_UNC_MAX);
                    while (pos < uncompressedData.Length)
                    {
                        var data = CompressChunk(uncompressedData, ref pos);
                        output.Write((ushort) (data.Length + 2));
                        output.Write(data);
                        output.Write((byte)chunkCount);
                        chunkCount--;
                    }
                    output.Flush();
                }

                return outputMemoryStream.ToArray();
            }
        }

        private static byte[] CompressChunk(byte[] data, ref int pos)
        {
            var CHUNK_SIZE_CMP_MAX = 0xFFC0;

            var startPos = pos;
            var endLimit = Math.Min(startPos + CHUNK_SIZE_UNC_MAX, data.Length);

            using (var outputMemoryStream = new MemoryStream())
            {
                using (var output = new CompressedBinaryWriter(outputMemoryStream))
                {
                    output.InitializeFlags();

                    while (pos < data.Length)
                    {
                        if (output.Length >= CHUNK_SIZE_CMP_MAX || pos - startPos >= CHUNK_SIZE_UNC_MAX)
                        {
                            break;
                        }
                        
                        var match = FindMatch(data, pos, startPos, endLimit);
                        var repeat = FindRepeat(data, pos, endLimit);

                        if (repeat.Length > 0 && match.Length > 0)
                        {
                            if (repeat.Length >= 0x40)
                            {
                                //Debug.WriteLine($"Repeat\t{repeat.Length}\t{match.Length}\t{match.Position}");
                                EncodeRepeat(output, data[pos], repeat.Length);
                                pos += repeat.Length;
                            }
                            else if (match.Length >= 0x40)
                            {
                                //Debug.WriteLine($"Match\t{match.Length}\t{match.Position}\t{repeat.Length}");
                                EncodeMatch(output, match.Length, match.Position);
                                pos += match.Length;
                            }
                            else if (match.Length >= repeat.Length)
                            {
                                //Debug.WriteLine($"Match\t{match.Length}\t{match.Position}\t{repeat.Length}");
                                EncodeMatch(output, match.Length, match.Position);
                                pos += match.Length;
                            }
                            else if (repeat.Length >= 0x0E)
                            {
                                //Debug.WriteLine($"Repeat\t{repeat.Length}\t{match.Length}\t{match.Position}");
                                EncodeRepeat(output, data[pos], repeat.Length);
                                pos += repeat.Length;
                            }
                            else
                            {
                                //Debug.WriteLine($"Match\t{match.Length}\t{match.Position}\t{repeat.Length}");
                                EncodeMatch(output, match.Length, match.Position);
                                pos += match.Length;
                            }
                        }
                        else if (repeat.Length > 0)
                        {
                            //Debug.WriteLine($"Repeat\t{repeat.Length}");
                            EncodeRepeat(output, data[pos], repeat.Length);
                            pos += repeat.Length;
                        }
                        else if (match.Length > 0)
                        {
                            //Debug.WriteLine($"Match\t{match.Length}\t{match.Position}");
                            EncodeMatch(output, match.Length, match.Position);
                            pos += match.Length;
                        }
                        else
                        {
                            output.Write(data[pos]);
                            pos++;
                            output.WriteFlag(false);
                        }
                    }

                    output.WriteFlagValue(0b110000, 6);

                    output.Write((byte) 0x00);
                    output.WriteFlag(false);
                }

                return outputMemoryStream.ToArray();
            }
        }

        private class MatchInfo
        {
            public int Length;
            public int Position;
        }

        private class RepeatInfo
        {
            public int Length;
        }

        private static MatchInfo FindMatch(byte[] data, int pos, int startLimit, int endLimit)
        {
            var result = new MatchInfo {Position = -1, Length = 0};

            var WINDOW_SIZE = 0x1FFF;
            var MIN_MATCH = 2;
            var maxMatch = 0xFF + 0x0E;

            var bestPos = 0;
            var bestLength = 1;

            var current = pos - 1;

            var startPos = Math.Max(pos - WINDOW_SIZE, startLimit);

            if (endLimit - pos <= 3)
            {
                return result;
            }

            while (current >= startPos)
            {
                if (data[current] == data[pos])
                {
                    var maxLength = Math.Min(endLimit - pos, maxMatch);

                    var length = DataCompare(data, current + 1, pos + 1, maxLength);
                    if (length > bestLength)
                    {
                        bestLength = length;
                        bestPos = current;
                    }
                }

                current--;
            }

            if (bestLength < MIN_MATCH)
            {
                // No se ha encontrado coincidencia o la longitud es menor que el mínimo
                return result;
            }

            result.Position = pos - bestPos;
            result.Length = bestLength;

            return result;
        }

        private static int DataCompare(byte[] input, int pos1, int pos2, int maxLength)
        {
            var length = 1;

            while (length < maxLength && input[pos1] == input[pos2])
            {
                pos1++;
                pos2++;
                length++;
            }

            return length;
        }

        private static RepeatInfo FindRepeat(byte[] data, int pos, int endLimit)
        {
            if (endLimit - pos <= 3)
            {
                return new RepeatInfo{Length = 0};
            }

            var maxMatch = 0x0FFF;
            
            var startPos = pos;
            var endPos = Math.Min(endLimit, pos + maxMatch);

            var bestLength = 0;

            var i = startPos;
            while (i < endPos && data[pos] == data[i])
            {
                bestLength++;
                i++;
            }

            var result = new RepeatInfo {Length = bestLength >= 3 ? bestLength : 0};

            return result;
        }

        private static void EncodeRepeat(CompressedBinaryWriter output, byte repeatByte, int size)
        {
            if (size < 0x0E)
            {
                output.Write(repeatByte);
                output.WriteFlag(false);
                EncodeMatch(output, size - 1, 1);
            }
            else
            {
                size -= 0x0E;

                output.WriteFlagValue(0b110000, 6);
                output.Write((byte) 0x01);
                output.WriteFlag(false);

                if (size < 0x10)
                {
                    output.WriteFlag(false);

                    var flagValue = ((size >> 3) & 0x01) == 0x01;
                    output.WriteFlag(flagValue);
                    flagValue = ((size >> 2) & 0x01) == 0x01;
                    output.WriteFlag(flagValue);
                    flagValue = ((size >> 1) & 0x01) == 0x01;
                    output.WriteFlag(flagValue);

                    output.Write(repeatByte);

                    flagValue = (size & 0x01) == 0x01;
                    output.WriteFlag(flagValue);
                }
                else
                {
                    var high = size >> 8;
                    var low = size & 0xFF;
                    output.WriteFlag(true);

                    var flagValue = ((high >> 3) & 0x01) == 0x01;
                    output.WriteFlag(flagValue);
                    flagValue = ((high >> 2) & 0x01) == 0x01;
                    output.WriteFlag(flagValue);
                    flagValue = ((high >> 1) & 0x01) == 0x01;
                    output.WriteFlag(flagValue);

                    output.Write((byte)low);
                    output.Write(repeatByte);

                    flagValue = (high & 0x01) == 0x01;
                    output.WriteFlag(flagValue);
                }
            }
        }

        private static void EncodeMatch(CompressedBinaryWriter output, int matchSize, int matchPos)
        {
            if (matchPos < 0x100)
            {
                output.WriteFlag(true);
                output.Write((byte) matchPos);
                output.WriteFlag(false);
            }
            else
            {
                var high = matchPos >> 8;
                var low = matchPos & 0xFF;

                output.WriteFlag(true);
                output.WriteFlag(true);

                var flagValue = ((high >> 4) & 0x01) == 0x01;
                output.WriteFlag(flagValue);
                flagValue = ((high >> 3) & 0x01) == 0x01;
                output.WriteFlag(flagValue);
                flagValue = ((high >> 2) & 0x01) == 0x01;
                output.WriteFlag(flagValue);
                flagValue = ((high >> 1) & 0x01) == 0x01;
                output.WriteFlag(flagValue);

                output.Write((byte)low);

                flagValue = (high & 0x01) == 0x01;
                output.WriteFlag(flagValue);
            }

            output.WriteCopyCount(matchSize);
        }
    }
}
