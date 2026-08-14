using System;
using System.IO;

namespace XCompression
{
    public sealed class ManagedDecompressionContext : IDisposable
    {
        private readonly LzxDecoder _decoder;
        private bool _disposed;

        public ManagedDecompressionContext(uint windowSize, uint chunkSize)
        {
            int windowBits = 0;
            while ((1u << windowBits) < windowSize)
                windowBits++;
            _decoder = new LzxDecoder(windowBits);
            _disposed = false;
        }

        public ErrorCode Decompress(
            byte[] inputBytes,
            int inputOffset,
            ref int inputCount,
            byte[] outputBytes,
            int outputOffset,
            ref int outputCount)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ManagedDecompressionContext));

            try
            {
                int totalOutput = 0;
                int offset = inputOffset;
                int endOffset = inputOffset + inputCount;
                int outOff = outputOffset;

                while (offset < endOffset)
                {
                    int hi = inputBytes[offset++];
                    int frameSize = 0x8000;
                    int blockSize;

                    if (hi == 0xFF)
                    {
                        if (offset + 4 > endOffset)
                            break;
                        frameSize = (inputBytes[offset] << 8) | inputBytes[offset + 1];
                        offset += 2;
                        blockSize = (inputBytes[offset] << 8) | inputBytes[offset + 1];
                        offset += 2;
                    }
                    else
                    {
                        if (offset + 1 > endOffset)
                            break;
                        blockSize = (hi << 8) | inputBytes[offset];
                        offset += 1;
                    }

                    if (blockSize == 0 || frameSize == 0)
                        break;

                    if (offset + blockSize > endOffset)
                        break;

                    using (var inputStream = new MemoryStream(inputBytes, offset, blockSize))
                    using (var outputStream = new MemoryStream(outputBytes, outOff, frameSize))
                    {
                        int result = _decoder.Decompress(inputStream, blockSize, outputStream, frameSize);
                        if (result != 0)
                        {
                            System.Console.Error.WriteLine($"[LZX] frame fail: block={blockSize} frame={frameSize} off={offset - inputOffset} firstFrame={totalOutput==0}");
                            return (ErrorCode)(-1);
                        }
                        totalOutput += (int)outputStream.Position;
                        outOff += (int)outputStream.Position;
                    }

                    offset += blockSize;
                }

                outputCount = totalOutput;
                inputCount = offset - inputOffset;
                return ErrorCode.None;
            }
            catch
            {
                return (ErrorCode)(-1);
            }
        }

        public void Dispose()
        {
            _disposed = true;
        }
    }
}
