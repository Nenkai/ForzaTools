using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Syroot.BinaryData;
using System.Buffers.Binary;

namespace ForzaTools.Decryptor
{
    public class TransformITAesCryptoStream : Stream
    {
        private Stream _baseStream;
        public TransformITCryptoProvider _provider;
        private uint _chunkSize;

        private byte[] _baseIV = new byte[0x10];
        private uint _lastChunkPad;
        private byte[] _headerHMac = new byte[0x10];

        private byte[] _currentIV = new byte[0x10];
        private long _chunkCount;
        private long _totalChunkedSize; 

        public byte[] _currentChunk;
        public long _positionWithinChunk = 0;
        public long _chunkIndex = 0;

        public long _fileLength;

        public uint LastChunkSize => _chunkSize - _lastChunkPad;

        // Overriden from base stream
        public override bool CanRead => true;

        public override bool CanSeek => throw new NotImplementedException();

        public override bool CanWrite => throw new NotImplementedException();

        /// <summary>
        /// Returns the file length, not the base stream's length.
        /// </summary>
        public override long Length => (_chunkSize * _chunkCount) - _chunkSize + LastChunkSize;

        public override long Position { get => _baseStream.Position; set => throw new NotImplementedException(); }

        /// <summary>
        /// Creates a new TransformIT AES Crypto Stream.
        /// </summary>
        /// <param name="baseStream"></param>
        /// <param name="provider"></param>
        /// <param name="chunkSize"></param>
        /// <param name="fileLen">Use this if you are reading a file that isn't the whole stream.</param>
        public TransformITAesCryptoStream(Stream baseStream, TransformITCryptoProvider provider, uint chunkSize, int fileLen = -1)
        {
            _baseStream = baseStream;
            _provider = provider;
            _chunkSize = chunkSize;

            if (fileLen != -1)
                _fileLength = fileLen;
            else
                _fileLength = baseStream.Length;

            // Read Header
            ReadHeader();
        }

        private void ReadHeader()
        {
            // Header (BaseIV + Last Chunk Size + HMac)
            // Base IV (0x00)
            _baseStream.Read(_baseIV);
            _baseIV.CopyTo(_currentIV.AsSpan());

            // Last Chunk Padding (0x10)
            _lastChunkPad = _baseStream.ReadUInt32();

            // HMac (0x14)
            _baseStream.Read(_headerHMac);

            // Calculate all sizes
            long chunkSizeIncludingIV = (_chunkSize + _provider.IVSize);
            long fileSizeNoHeader = _fileLength - (0x10 + 0x04 + 0x10); // IV + Pad Size + HMac
            long _totalChunkedSize = fileSizeNoHeader / (_chunkSize + _provider.IVSize) * _chunkSize; // Total size of chunks, IV and header excluded 

            _chunkCount = (fileSizeNoHeader / chunkSizeIncludingIV);

            // Create integrity header
            Span<byte> headerIntegrity = new byte[(int)(4 + _provider.IVSize + 4)]; // totalChunkedSize + IV + lastChunkPad
            BinaryPrimitives.WriteUInt32LittleEndian(headerIntegrity, (uint)_totalChunkedSize);
            _baseIV.CopyTo(headerIntegrity[0x04..]);
            BinaryPrimitives.WriteUInt32LittleEndian(headerIntegrity[0x14..], _lastChunkPad);

            if (!_provider.Authenticate(headerIntegrity, 0x18, _headerHMac))
            {
                Console.WriteLine("WARNING: Could not authenticate stream header with Mac Key");
            }
            else
            {
                Console.WriteLine("Stream header successfully authenticated");
            }
        }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_currentChunk is null)
            {
                _currentChunk = new byte[_chunkSize];
                ReadNextChunk(first: true);
            }

            int chunkRem = GetRemainingInChunk();

            int bytesConsumed = 0;

            if (count > chunkRem)
            {
                // Read remaining of current block
                _currentChunk.AsSpan((int)_positionWithinChunk, chunkRem).CopyTo(buffer.AsSpan(offset));
                bytesConsumed += chunkRem;

                offset += chunkRem;
                count -= chunkRem;
                _positionWithinChunk = _chunkSize;
                
                while (count > 0 && !IsEndOfStream())
                {
                    // Start new chunk if needed
                    if (_positionWithinChunk >= _chunkSize)
                    {
                        ReadNextChunk();
                    }

                    chunkRem = GetRemainingInChunk();

                    int toRead = Math.Min(chunkRem, count);
                    Memcpy(buffer.AsSpan(offset), _currentChunk[(int)_positionWithinChunk..], toRead);

                    _positionWithinChunk += toRead;
                    offset += toRead;

                    count -= toRead;
                    bytesConsumed += toRead;
                }

                return bytesConsumed;
            }
            else
            {
                // Read from current chunk
                Memcpy(buffer.AsSpan(offset), _currentChunk[(int)_positionWithinChunk..], count);
                _positionWithinChunk += count;

                return count;
            }
        }

        private void ReadNextChunk(bool first = false)
        {
            _positionWithinChunk = 0;
            if (!first)
                _chunkIndex++;

            // Read ciphertext from basestream
            _baseStream.Read(_currentChunk, 0, (int)_chunkSize);

            // Read next IV (also used for content validation).
            byte[] nextIV = new byte[0x10];
            _baseStream.Read(nextIV);

            uint bytesToRead = _chunkIndex == _chunkCount - 1 ? LastChunkSize : _chunkSize;

            // Decrypt TFIT encrypted data.
            _provider.TFIT_wbaes_cbc_decrypt(_provider.Key, _currentChunk, _chunkSize, _currentIV, _currentChunk);

            // Cipher next IV to create expected HMAC for current chunk.
            Span<byte> currentChunkHMac = stackalloc byte[(int)_provider.IVSize];
            _provider.TFIT_wbaes_cbc_decrypt(_provider.Key, nextIV, _provider.IVSize, _currentIV, currentChunkHMac);

            // Verify HMAC.
            if (!_provider.Authenticate(_currentChunk, bytesToRead, currentChunkHMac))
            {
                // TODO: Doesn't work with last chunks, to be figured
                // Commented for now
            }

            _currentIV = nextIV;
        }

        private int GetRemainingInChunk()
        {
            if (_chunkIndex != _chunkCount - 1)
                return (int)(_chunkSize - _positionWithinChunk);
            else
                return (int)(LastChunkSize - _positionWithinChunk);
        }

        private void Memcpy(Span<byte> output, Span<byte> input, int len)
        {
            input[..len].CopyTo(output[..len]);
        }

        private bool IsEndOfStream()
        {
            return _chunkIndex == _chunkCount - 1 && _positionWithinChunk == LastChunkSize;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override void Close()
        {
            _baseStream.Close();
            base.Close();
        }
    }
}
