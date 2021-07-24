using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace Collection
{
    class PersistentCollection<T> : ICollection<T>, IEnumerator<T>, IList<T>
    {
        // Create a collection of values that is actually a file
        // Need to consider locking as read and writes may conflict
        // Need to understand what thread safe means fully
        // Start simple and just have it as a string list
        // assume that the file is stored with the exe / dll
        // will need some form of index as will want to remove items
        //
        // Header
        // ------
        //
        // 00 - unsigned int16 - number of elements _size
        // 00 - unsigned int16 - pointer to current element _pointer
        //
        // Data assuming string (Value)
        //
        // 0 - unsigned byte - flag 1 = deleted, 2 = Spare
        //
        // 00 - leb128 - Length of element handled by the binary writer and reader in LEB128 format
        // bytes - string
        // ...
        // 00 - leb128 - Length of element handled by the binary writer and reader in LEB128 format
        // bytes - string
        //
        // Index
        // -----
        //
        // 00 - unsigned int16 - pointer to data
        // 00 - unsigned int16 - length of data
        // ...
        // 00 - unsigned int16 - pointer to data + 1 
        // 00 - unsigned int16 - length of data + 1

        #region Fields

        string _path = "";
        string _name = "PersistentCollection";
        // Lets assume we will add the extension automatically but filename is not correct 

        readonly object _lockObject = new Object();
        UInt16 _size;
        UInt16 _pointer;
        UInt16 _data = 4;
        int _cursor;
        private bool disposedValue;

        #endregion
        #region Constructors

        public PersistentCollection()
        {
            Open(_path, _name, false);
        }

        public PersistentCollection(bool reset)
        {
            Open(_path, _name, reset);
        }

        public PersistentCollection(string filename)
        {
            _name = filename;
            Open(_path, _name, false);
        }

        public PersistentCollection(string path, string filename)
        {
            _name = filename;
            _path = path;
            Open(_path, _name, false);
        }
        public PersistentCollection(string path, string filename, bool reset)
        {
            _name = filename;
            _path = path;
            Open(_path, _name, reset);
        }

        #endregion
        #region Proprties

        public int Count
        {
            get
            {
                return (_size);
            }
        }

        public string Path
        {
            get
            {
                return (_path);
            }
            set
            {
                _path = value;
            }
        }

        public string Name
        {
            get
            {
                return (_name);
            }
            set
            {
                _name = value;
            }
        }

        // Make the indexer property.
        public T this[int index]
        {
            get
            {
                // Could start to simplify here and use the private Read() method

                object data = null;
                lock (_lockObject)
                {
                    if ((index >=0) && (index < _size))
                    {
                        Type ParameterType = typeof(T);
                        string filenamePath = System.IO.Path.Combine(_path, _name);
                        // Need to search the index file

                        BinaryReader indexReader = new BinaryReader(new FileStream(filenamePath + ".idx", FileMode.Open));
                        BinaryReader binaryReader = new BinaryReader(new FileStream(filenamePath + ".bin", FileMode.Open));
                        indexReader.BaseStream.Seek(index * 4, SeekOrigin.Begin);                               // Get the pointer from the index file
                        UInt16 pointer = indexReader.ReadUInt16();                                              // Reader the pointer from the index file
                        binaryReader.BaseStream.Seek(_data + pointer, SeekOrigin.Begin);                                // Move to the correct location in the data file
                        byte flag = binaryReader.ReadByte();
                        if (ParameterType == typeof(string))
                        {
                            data = binaryReader.ReadString();
                        }
                        else
                        {
                            data = default(T);
                        }
                        binaryReader.Close();
                        indexReader.Close();
                        return ((T)Convert.ChangeType(data, typeof(T)));
                    }
                    else
                    {
                        throw new IndexOutOfRangeException();
                    }
                }
            }

            set
            {
                // Need to update the item at the index
                // This is more complex for strings if the new string is longer than the
                // available space from the previous string. Just occred to me that 
                // might be a good idea to store the orinal length or space as new 
                // strings might end of getting shorter and shorter

                lock (_lockObject)
                {
                    if ((index >= 0) && (index < _size))
                    {
                        Type ParameterType = typeof(T);
                        string filenamePath = System.IO.Path.Combine(_path, _name);

                        BinaryReader indexReader = new BinaryReader(new FileStream(filenamePath + ".idx", FileMode.Open));
                        indexReader.BaseStream.Seek(index * 4, SeekOrigin.Begin);                               // Get the index pointer
                        UInt16 pointer = indexReader.ReadUInt16();                                              // Read the pointer from the index file
                        int offset = indexReader.ReadUInt16();                                               // Read the length from the index file
                        indexReader.Close();

                        BinaryWriter binaryWriter = new BinaryWriter(new FileStream(filenamePath + ".bin", FileMode.OpenOrCreate));
                        int length = 0;
                        length = length + 1;    // Including the flag
                        if (ParameterType == typeof(string))
                        {
                            int l = (UInt16)Convert.ToString(value).Length;
                            length = length + LEB128.Size(l) + l;  // Includes the byte length parameter
                                                                   // ** need to watch this as can be 2 bytes if length is > 127 characters
                            if (offset > length)
                            {
                                // If there is space write the data
                                binaryWriter.Seek(_data + pointer, SeekOrigin.Begin);
                                byte flag = 0;
                                binaryWriter.Write(flag);
                                string s = Convert.ToString(value);
                                binaryWriter.Write(s);
                            }
                            else
                            {
                                // There is no space so flag the record to indicate its spare
                                binaryWriter.Seek(_data + pointer, SeekOrigin.Begin);
                                byte flag = 2;
                                binaryWriter.Write(flag);

                                // Overwrite the index to use the new location at the end of the file
                                BinaryWriter indexWriter = new BinaryWriter(new FileStream(filenamePath + ".idx", FileMode.Open));
                                indexWriter.Seek(index * 4, SeekOrigin.Begin);   // Get the index pointer
                                indexWriter.Write(_pointer);
                                indexWriter.Close();

                                // Write the header

                                binaryWriter.Seek(0, SeekOrigin.Begin);     // Move to start of the file
                                binaryWriter.Write(_size);                  // Write the size
                                _pointer = (UInt16)(_pointer + length);     //
                                binaryWriter.Write(_pointer);               // Write the pointer
                                binaryWriter.Close();

                                // Write the data

                                // Appending will only work if the file is deleated and the updates start again
                                // Not sure if this is the best approach.
                                // With strings might have to do the write first and then update the pointer.

                                binaryWriter = new BinaryWriter(new FileStream(filenamePath + ".bin", FileMode.Append));
                                flag = 0;
                                binaryWriter.Write(flag);
                                string s = Convert.ToString(value);
                                binaryWriter.Write(s);
                            }
                        }
                        else
                        {
                            // Test
                        }
                        binaryWriter.Close();
                    }
                    else
                    {
                        throw new IndexOutOfRangeException();
                    }
                }
            }
        }

        public bool IsReadOnly
        {
            get
            {
                return (false);
            }
        }

        #endregion
        #region Methods

        /// <summary>
        /// Clear the Queue
        /// </summary>
        public void Clear()
        {
            lock (_lockObject)
            {
                Reset(_path, _name);
            }
        }

        /// <summary>
        /// Add a new item at the end of the list
        /// </summary>
        /// <param name="item"></param>
        public void Add(T item)
        {
            string filenamePath = System.IO.Path.Combine(_path, _name);
            lock (_lockObject)
            {
                Type ParameterType = typeof(T);

                // append the new pointer the index file

                BinaryWriter indexWriter = new BinaryWriter(new FileStream(filenamePath + ".idx", FileMode.Append));
                indexWriter.Write(_pointer);  // Write the pointer of the previous location

                // Need to consider how data is stored
                // so if int, string
                // calculate the new pointers

                int offset = 0;
                offset = offset + 1;    // Including the flag
                if (ParameterType == typeof(string))
                {
                    int length = Convert.ToString(item).Length;
                    offset = offset + LEB128.Size(length) + length; // Includes the byte length parameter
                                                                    // ** need to watch this as can be 2 bytes if length is > 127 characters
                                                                    // ** https://en.wikipedia.org/wiki/LEB128
                }

                indexWriter.Write((UInt16)offset);
                indexWriter.Close();

                // Write the header

                BinaryWriter binaryWriter = new BinaryWriter(new FileStream(filenamePath + ".bin", FileMode.OpenOrCreate));
                binaryWriter.Seek(0, SeekOrigin.Begin); // Move to start of the file
                _size++;
                binaryWriter.Write(_size);                  // Write the size
                _pointer = (UInt16)(_pointer + offset);     //
                binaryWriter.Write(_pointer);               // Write the pointer
                binaryWriter.Close();

                // Write the data

                // Appending will only work if the file is deleated and the updates start again
                // Not sure if this is the best approach.
                // With strings might have to do the write first and then update the pointer.

                binaryWriter = new BinaryWriter(new FileStream(filenamePath + ".bin", FileMode.Append));
                byte flag = 0;
                binaryWriter.Write(flag);
                if (ParameterType == typeof(string))
                {
                    string s = Convert.ToString(item);
                    binaryWriter.Write(s);
                }
                binaryWriter.Close();
            }
        }

        /// <summary>
        /// Remove item from the list
        /// </summary>
        /// <param name="item"></param>
        public bool Remove(T item)
        {
            bool removed = false;

            string filenamePath = System.IO.Path.Combine(_path, _name);

            lock (_lockObject)
            {
                Type ParameterType = typeof(T);

                // Logic is probably to open the index
                // work through this and identify the data position in the file (note zero means that data is delted)
                // read the data
                // check if the data matches
                // remove the data
                // update the index file by removing the refernce

                object data;
                BinaryReader binaryReader = new BinaryReader(new FileStream(filenamePath + ".bin", FileMode.Open));
                BinaryReader indexReader = new BinaryReader(new FileStream(filenamePath + ".idx", FileMode.Open));
                int index = -1;
                UInt16 pointer = 0;
                for (int counter = 0; counter < _size; counter++)
                {
                    indexReader.BaseStream.Seek(counter * 4, SeekOrigin.Begin);                               // Get the index pointer
                    pointer = indexReader.ReadUInt16();                                              // Read the pointer from the index file
                    UInt16 offset = indexReader.ReadUInt16();

                    binaryReader.BaseStream.Seek(_data + pointer, SeekOrigin.Begin);                                // Move to the correct location in the data file
                    byte flag = binaryReader.ReadByte();
                    if (ParameterType == typeof(string))
                    {
                        data = binaryReader.ReadString();
                        if ((string)data == (string)Convert.ChangeType(item, typeof(string)))
                        {
                            index = counter;
                            // Need to store index, pointer
                            break;
                        }
                    }
                    else
                    {
                        data = default(T);
                    }
                }
                binaryReader.Close();
                indexReader.Close();

                if (index == -1)
                {
                    throw new KeyNotFoundException();
                }
                else
                {
                    // Write the header

                    BinaryWriter binaryWriter = new BinaryWriter(new FileStream(filenamePath + ".bin", FileMode.OpenOrCreate));
                    binaryWriter.Seek(0, SeekOrigin.Begin); // Move to start of the file
                    _size--;
                    binaryWriter.Write(_size);                  // Write the size

                    // Flag the record to indicate its deleted

                    binaryWriter.Seek(_data + pointer, SeekOrigin.Begin);
                    byte flag = 1;
                    binaryWriter.Write(flag);
                    binaryWriter.Close();

                    // Overwrite the index

                    FileStream stream = new FileStream(filenamePath + ".idx", FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                    indexReader = new BinaryReader(stream);
                    BinaryWriter indexWriter = new BinaryWriter(stream);

                    // copy the ponter and length data downwards 

                    for (int counter = index; counter < _size; counter++)
                    {
                        indexReader.BaseStream.Seek((counter + 1) * 4, SeekOrigin.Begin); // Move to location of the index
                        pointer = indexReader.ReadUInt16();                                              // Read the pointer from the index file
                        UInt16 offset = indexReader.ReadUInt16();
                        indexWriter.Seek(counter * 4, SeekOrigin.Begin); // Move to location of the index
                        indexWriter.Write(pointer);
                        indexWriter.Write(offset);
                    }
                    indexWriter.BaseStream.SetLength(_size * 4);    // Trim the file as Add uses append
                    indexWriter.Close();
                    indexReader.Close();
                    stream.Close();

                    removed = true;

                }
            }
            return (removed);
        }

        public void RemoveAt(int index)
        {
            string filenamePath = System.IO.Path.Combine(_path, _name);
            if ((index >= 0) && (index < _size))
            {
                lock (_lockObject)
                {
                    Type ParameterType = typeof(T);
                    UInt16 pointer = 0;
                    BinaryReader indexReader = new BinaryReader(new FileStream(filenamePath + ".idx", FileMode.Open));
                    indexReader.BaseStream.Seek(index * 4, SeekOrigin.Begin);                               // Get the index pointer
                    pointer = indexReader.ReadUInt16();
                    indexReader.Close();

                    // Write the header

                    BinaryWriter binaryWriter = new BinaryWriter(new FileStream(filenamePath + ".bin", FileMode.OpenOrCreate));
                    binaryWriter.Seek(0, SeekOrigin.Begin);     // Move to start of the file
                    _size--;
                    binaryWriter.Write(_size);                  // Write the size

                    // flag the record to indicate its deleted

                    binaryWriter.Seek(_data + pointer, SeekOrigin.Begin);
                    byte flag = 1;
                    binaryWriter.Write(flag);
                    binaryWriter.Close();

                    // Overwrite the index

                    FileStream stream = new FileStream(filenamePath + ".idx", FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                    indexReader = new BinaryReader(stream);
                    BinaryWriter indexWriter = new BinaryWriter(stream);

                    // copy the ponter and length data downwards 

                    for (int counter = index; counter < _size; counter++)
                    {
                        indexReader.BaseStream.Seek((counter + 1) * 4, SeekOrigin.Begin); // Move to location of the index
                        pointer = indexReader.ReadUInt16();                                              // Read the pointer from the index file
                        UInt16 offset = indexReader.ReadUInt16();
                        indexWriter.Seek(counter * 4, SeekOrigin.Begin); // Move to location of the index
                        indexWriter.Write(pointer);
                        indexWriter.Write(offset);
                    }
                    indexWriter.BaseStream.SetLength(_size * 4);    // Trim the file as Add uses append
                    indexWriter.Close();
                    indexReader.Close();
                    stream.Close();
                }
            }
            else
            {
                throw new IndexOutOfRangeException();
            }
        }

        public void Insert(int index, T item)
        {
            if ((index >= 0) && (index < _size))
            {
                Write(_path, _name, index, item);
            }
            else
            {
                throw new IndexOutOfRangeException();
            }
        }

        bool IEnumerator.MoveNext()
        {
            bool moved = false;
            if (_cursor < _size)
            {
                moved = true;
            }
            return (moved);
        }

        void IEnumerator.Reset()
        {
            _cursor = -1;
        }

        object IEnumerator.Current
        {
            get
            {
                if ((_cursor < 0) || (_cursor == _size))
                {
                    throw new InvalidOperationException();
                }
                else
                {
                    return (Read(_path, _name, _cursor));
                }
            }
        }

        T IEnumerator<T>.Current
        {
            get
            {
                if ((_cursor < 0) || (_cursor == _size))
                {
                    throw new InvalidOperationException();
                }
                else
                {
                    return ((T)Convert.ChangeType(Read(_path, _name, _cursor), typeof(T)));
                }
            }
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            throw new NotImplementedException();
        }

        public IEnumerator GetEnumerator()
        {
            for (int cursor = 0; cursor < _size; cursor++)
            {
                // Return the current element and then on next function call 
                // resume from next element rather than starting all over again;
                yield return (Read(_path, _name, cursor));
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~PersistentQueue()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        void IDisposable.Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }


        public int IndexOf(T item)
        {
            int index = -1;
            for (int i = 0; i < _size; ++i)
            {
                T value = (T)Read(_path, _name, i);
                if (item.Equals(value))
                {
                    index = i;
                    break;
                }
            }
            return index;
        }

        public bool Contains(T item)
        {
            bool match = false;
            for (int i = 0; i < _size; ++i)
            {
                T value = (T)Read(_path, _name, i);
                if (item.Equals(value))
                {
                    match = true;
                    break;
                }
            }
            return match;
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }


        #endregion
        #region Private

        private void Open(string path, string filename, bool reset)
        {
            string filenamePath = System.IO.Path.Combine(path, filename);
            if ((File.Exists(filenamePath + ".bin") == true) && (reset == false))
            {
                // Assume we only need to read the data and not the index
                BinaryReader binaryReader = new BinaryReader(new FileStream(filenamePath + ".bin", FileMode.Open));
                _size = binaryReader.ReadUInt16();
                _pointer = binaryReader.ReadUInt16();
                binaryReader.Close();
            }
            else
            {
                // Need to delete both data and index
                File.Delete(filenamePath + ".bin");
                // Assumption here is the the index also exists
                File.Delete(filenamePath + ".idx");
                Reset(path, filename);
            }
        }

        private void Reset(string path, string filename)
        {
            // Reset the file
            string filenamePath = System.IO.Path.Combine(path, filename);
            BinaryWriter binaryWriter = new BinaryWriter(new FileStream(filenamePath + ".bin", FileMode.OpenOrCreate));
            binaryWriter.Seek(0, SeekOrigin.Begin); // Move to start of the file
            _size = 0;
            _pointer = 0;                           // Start of the data now offset by _data
            binaryWriter.Write(_size);              // Write the new size
            binaryWriter.Write(_pointer);           // Write the new pointer
            binaryWriter.BaseStream.SetLength(4);   // Fix the size as we are resetting
            binaryWriter.Close();

            // Create the index

            binaryWriter = new BinaryWriter(new FileStream(filenamePath + ".idx", FileMode.OpenOrCreate));
            binaryWriter.BaseStream.SetLength(0);
            binaryWriter.Close();

        }

        private object Read(string path, string filename, int index)
        {
            object data = null;
            lock (_lockObject)
            {

                Type ParameterType = typeof(T);
                string filenamePath = System.IO.Path.Combine(path, filename);
                // Need to search the index file

                BinaryReader indexReader = new BinaryReader(new FileStream(filenamePath + ".idx", FileMode.Open));
                BinaryReader binaryReader = new BinaryReader(new FileStream(filenamePath + ".bin", FileMode.Open));
                indexReader.BaseStream.Seek(index * 4, SeekOrigin.Begin);                               // Get the pointer from the index file
                UInt16 pointer = indexReader.ReadUInt16();                                              // Reader the pointer from the index file
                binaryReader.BaseStream.Seek(_data + pointer, SeekOrigin.Begin);                                // Move to the correct location in the data file

                byte flag = binaryReader.ReadByte();
                if (ParameterType == typeof(string))
                {
                    data = binaryReader.ReadString();
                }
                else
                {
                    data = default(T);
                }
                binaryReader.Close();
                indexReader.Close();
            }
            return (data);
        }

        private void Write(string path, string filename, int index, object item)
        {
            lock (_lockObject)
            {

                Type ParameterType = typeof(T);
                string filenamePath = System.IO.Path.Combine(path, filename);

                // Write the data

                // Appending will only work if the file is deleated and the updates start again
                // Not sure if this is the best approach.
                // With strings might have to do the write first and then update the pointer.

                BinaryWriter binaryWriter = new BinaryWriter(new FileStream(filenamePath + ".bin", FileMode.Append));

                int offset = 0;
                offset = offset + 1;    // Including the flag
                if (ParameterType == typeof(string))
                {
                    int l = (UInt16)Convert.ToString(item).Length;
                    offset = offset + LEB128.Size(l) + l;           // Includes the byte length parameter
                                                                    // ** need to watch this as can be 2 bytes if length is > 127 characters
                                                                    // ** https://en.wikipedia.org/wiki/LEB128
                }

                byte flag = 0;
                binaryWriter.Write(flag);
                if (ParameterType == typeof(string))
                {
                    string s = Convert.ToString(item);
                    binaryWriter.Write(s);
                }
                binaryWriter.Close();

                // Write the header

                binaryWriter = new BinaryWriter(new FileStream(filenamePath + ".bin", FileMode.OpenOrCreate));
                binaryWriter.Seek(0, SeekOrigin.Begin);                         // Move to start of the file
                _size++;
                binaryWriter.Write(_size);                                      // Write the size
                binaryWriter.Write((UInt16)(_pointer + offset));               // Write the pointer
                binaryWriter.Close();

                // need to insert the ponter as a new entry in the index

                FileStream stream = new FileStream(filenamePath + ".idx", FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                BinaryReader indexReader = new BinaryReader(stream);
                BinaryWriter indexWriter = new BinaryWriter(stream);

                UInt16 position;
                for (int counter = _size - 1; counter > index; counter--)
                {
                    position = (UInt16)((counter - 1) * 4);
                    indexReader.BaseStream.Seek(position, SeekOrigin.Begin);       // Move to location of the index
                    UInt16 pointer = indexReader.ReadUInt16();                              // Read the pointer from the index file
                    UInt16 off = indexReader.ReadUInt16();
                    position = (UInt16)(counter * 4);
                    indexWriter.Seek(counter * 4, SeekOrigin.Begin);                        // Move to location of the index
                    indexWriter.Write(pointer);
                    indexWriter.Write(off);
                }
                position = (UInt16)(index * 4);
                indexWriter.Seek(position, SeekOrigin.Begin);                        // Move to location of the index
                indexWriter.Write(_pointer);
                indexWriter.Write((UInt16)offset);
                indexWriter.Close();
                indexReader.Close();
                stream.Close();
            }
        }


        #endregion
    }

    public static class LEB128
    {
        public static byte[] Encode(int value)
        {
            byte[] data = new byte[5];  // Assume 32 bit max as its an int32
            int size = 0;
            do
            {
                byte byt = (byte)(value & 0x7f);
                value >>= 7;
                if (value != 0)
                {
                    byt = (byte)(byt | 128);
                }
                data[size] = byt;
                size = size + 1;
            } while (value != 0);
            return (data);
        }

        public static int Size(int value)
        {
            int size = 0;
            do
            {
                byte byt = (byte)(value & 0x7f);
                value >>= 7;
                size = size + 1;
            } while (value != 0);
            return (size);
        }
    }
}

