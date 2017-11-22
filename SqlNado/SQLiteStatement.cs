﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using SqlNado.Utilities;

namespace SqlNado
{
    public class SQLiteStatement : IDisposable
    {
        private IntPtr _handle;
        private static readonly byte[] ZeroBytes = new byte[0];
        private Dictionary<string, int> _columnsIndices;
        private string[] _columnsNames;

        public SQLiteStatement(SQLiteDatabase database, string sql)
        {
            if (database == null)
                throw new ArgumentNullException(nameof(database));

            if (sql == null)
                throw new ArgumentNullException(nameof(sql));

            Database = database;
            Sql = sql;
            database.Log(TraceLevel.Verbose, "Preparing statement `" + sql + "`", nameof(SQLiteStatement) + ".ctor");
            database.CheckError(SQLiteDatabase._sqlite3_prepare16_v2(database.CheckDisposed(), sql, sql.Length * 2, out _handle, IntPtr.Zero));
        }

        [Browsable(false)]
        public SQLiteDatabase Database { get; }
        [Browsable(false)]
        public IntPtr Handle => _handle;
        public string Sql { get; }

        public string[] ColumnsNames
        {
            get
            {
                if (_columnsNames == null)
                {
                    _columnsNames = new string[ColumnCount];
                    if (_handle != IntPtr.Zero)
                    {
                        for (int i = 0; i < _columnsNames.Length; i++)
                        {
                            _columnsNames[i] = GetColumnName(i);
                        }
                    }
                }
                return _columnsNames;
            }
        }

        public IReadOnlyDictionary<string, int> ColumnsIndices
        {
            get
            {
                if (_columnsIndices == null)
                {
                    _columnsIndices = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    if (_handle != IntPtr.Zero)
                    {
                        int count = ColumnCount;
                        for (int i = 0; i < count; i++)
                        {
                            string name = GetColumnName(i);
                            if (name != null)
                            {
                                _columnsIndices[name] = i;
                            }
                        }
                    }
                }
                return _columnsIndices;
            }
        }

        public virtual int ParameterCount => SQLiteDatabase._sqlite3_bind_parameter_count(CheckDisposed());
        public virtual int ColumnCount => SQLiteDatabase._sqlite3_column_count(CheckDisposed());

        public virtual void BindParameter(string name, object value)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));

            int index = GetParameterIndex(name);
            if (index == 0)
                throw new SqlNadoException("0005: Parameter '" + name + "' was not found.");

            BindParameter(index, value);
        }

        public virtual void BindParameter(int index, object value)
        {
            SQLiteErrorCode code;
            var type = Database.GetBindType(value); // never null
            var ctx = Database.CreateBindContext();
            ctx.Statement = this;
            ctx.Value = value;
            ctx.Index = index;
            object bindValue = type.ConvertFunc(ctx);
            if (bindValue == null)
            {
                Database.Log(TraceLevel.Verbose, "Index " + index + " as null");
                code = BindParameterNull(index);
            }
            else if (bindValue is string s)
            {
                Database.Log(TraceLevel.Verbose, "Index " + index + " as String: " + s);
                code = BindParameter(index, s);
            }
            else if (bindValue is int i)
            {
                Database.Log(TraceLevel.Verbose, "Index " + index + " as Int32: " + i);
                code = BindParameter(index, i);
            }
            else if (bindValue is long l)
            {
                Database.Log(TraceLevel.Verbose, "Index " + index + " as Int64: " + l);
                code = BindParameter(index, l);
            }
            else if (bindValue is bool b)
            {
                Database.Log(TraceLevel.Verbose, "Index " + index + " as Boolean: " + b);
                code = BindParameter(index, b);
            }
            else if (bindValue is double d)
            {
                Database.Log(TraceLevel.Verbose, "Index " + index + " as Double: " + d);
                code = BindParameter(index, d);
            }
            else if (bindValue is byte[] bytes)
            {
                Database.Log(TraceLevel.Verbose, "Index " + index + " as Byte[]: " + Conversions.ToHexa(bytes, 32));
                code = BindParameter(index, bytes);
            }
            else if (bindValue is ISQLiteBlobObject blob)
            {
                if (blob.TryGetData(out bytes))
                {
                    Database.Log(TraceLevel.Verbose, "Index " + index + " as Byte[] from ISQLiteBlobObject: " + Conversions.ToHexa(bytes, 32));
                    code = BindParameter(index, bytes);
                }
                else
                {
                    Database.Log(TraceLevel.Verbose, "Index " + index + " as empty Byte[] from ISQLiteBlobObject");
                    code = BindParameter(index, ZeroBytes);
                }
            }
            else if (bindValue is SQLiteZeroBlobParameter zbp)
            {
                Database.Log(TraceLevel.Verbose, "Index " + index + " as SQLiteZeroBlobParameter: " + zbp.Size);
                code = BindParameterZeroBlob(index, zbp.Size);
            }
            else
                throw new SqlNadoException("0010: Binding only supports Int32, Int64, String, Boolean, Double and Byte[] primitive types.");

            Database.CheckError(code);
        }

        // https://sqlite.org/c3ref/bind_blob.html
        public SQLiteErrorCode BindParameter(int index, string value)
        {
            if (value == null)
                return BindParameterNull(index);

            return SQLiteDatabase._sqlite3_bind_text16(CheckDisposed(), index, value, value.Length * 2, IntPtr.Zero);
        }

        public SQLiteErrorCode BindParameter(int index, byte[] value)
        {
            if (value == null)
                return BindParameterNull(index);

            return SQLiteDatabase._sqlite3_bind_blob(CheckDisposed(), index, value, value.Length, IntPtr.Zero);
        }

        public SQLiteErrorCode BindParameter(int index, bool value) => SQLiteDatabase._sqlite3_bind_int(CheckDisposed(), index, value ? 1 : 0);
        public SQLiteErrorCode BindParameter(int index, int value) => SQLiteDatabase._sqlite3_bind_int(CheckDisposed(), index, value);
        public SQLiteErrorCode BindParameter(int index, long value) => SQLiteDatabase._sqlite3_bind_int64(CheckDisposed(), index, value);
        public SQLiteErrorCode BindParameter(int index, double value) => SQLiteDatabase._sqlite3_bind_double(CheckDisposed(), index, value);
        public SQLiteErrorCode BindParameterNull(int index) => SQLiteDatabase._sqlite3_bind_null(CheckDisposed(), index);
        public SQLiteErrorCode BindParameterZeroBlob(int index, int size) => SQLiteDatabase._sqlite3_bind_zeroblob(CheckDisposed(), index, size);

        public virtual IEnumerable<object> BuildRow()
        {
            for (int i = 0; i < ColumnCount; i++)
            {
                yield return GetColumnValue(i);
            }
        }

        public int GetParameterIndex(string name)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));

            return SQLiteDatabase._sqlite3_bind_parameter_index(CheckDisposed(), name);
        }

        public virtual void ClearBindings() => SQLiteDatabase._sqlite3_clear_bindings(CheckDisposed());
        public virtual void Reset() => SQLiteDatabase._sqlite3_reset(CheckDisposed());

        protected internal IntPtr CheckDisposed()
        {
            var handle = _handle;
            if (handle == IntPtr.Zero)
                throw new ObjectDisposedException(nameof(Handle));

            return handle;
        }

        public string GetColumnString(int index)
        {
            var ptr = SQLiteDatabase._sqlite3_column_text16(CheckDisposed(), index);
            return ptr == IntPtr.Zero ? null : Marshal.PtrToStringUni(ptr);
        }

        public long GetColumnInt64(int index) => SQLiteDatabase._sqlite3_column_int64(CheckDisposed(), index);
        public int GetColumnInt32(int index) => SQLiteDatabase._sqlite3_column_int(CheckDisposed(), index);
        public double GetColumnDouble(int index) => SQLiteDatabase._sqlite3_column_double(CheckDisposed(), index);

        public virtual byte[] GetColumnByteArray(int index)
        {
            var handle = CheckDisposed();
            IntPtr ptr = SQLiteDatabase._sqlite3_column_blob(handle, index);
            if (ptr == IntPtr.Zero)
                return null;

            int count = SQLiteDatabase._sqlite3_column_bytes(handle, index);
            var bytes = new byte[count];
            Marshal.Copy(ptr, bytes, 0, count);
            return bytes;
        }

        public bool TryGetColumnValue(string name, out object value)
        {
            int i = GetColumnIndex(name);
            if (i < 0)
            {
                value = null;
                return false;
            }

            value = GetColumnValue(i);
            return true;
        }

        public virtual string GetNullifiedColumnValue(string name)
        {
            int i = GetColumnIndex(name);
            if (i < 0)
                return null;

            var value = GetColumnValue(i);
            if (value == null)
                return null;

            if (value is byte[] bytes)
                return Conversions.ToHexa(bytes).Nullify();

            return string.Format(CultureInfo.InvariantCulture, "{0}", value).Nullify();
        }

        public object GetColumnValue(string name)
        {
            int i = GetColumnIndex(name);
            if (i < 0)
                return null;

            return GetColumnValue(i);
        }

        public virtual object GetColumnValue(int index)
        {
            CheckDisposed();
            object value;
            SQLiteColumnType type = GetColumnType(index);
            switch (type)
            {
                case SQLiteColumnType.BLOB:
                    byte[] bytes = GetColumnByteArray(index);
                    value = bytes;
                    break;

                case SQLiteColumnType.TEXT:
                    string s = GetColumnString(index);
                    value = s;
                    break;

                case SQLiteColumnType.REAL:
                    double d = GetColumnDouble(index);
                    value = d;
                    break;

                case SQLiteColumnType.INTEGER:
                    long l = GetColumnInt64(index);
                    if (l >= int.MinValue && l <= int.MaxValue)
                    {
                        value = (int)l;
                    }
                    else
                    {
                        value = l;
                    }
                    break;

                //case SQLiteColumnType.SQLITE_NULL:
                default:
                    value = null;
                    break;
            }
            return value;
        }

        public virtual T GetColumnValue<T>(string name, T defaultValue)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));

            int index = GetColumnIndex(name);
            if (index < 0)
                return defaultValue;

            return GetColumnValue(index, defaultValue);
        }

        public virtual T GetColumnValue<T>(int index, T defaultValue)
        {
            object rawValue = GetColumnValue(index);
            if (!Conversions.TryChangeType(rawValue, CultureInfo.InvariantCulture, out T value))
                return defaultValue;

            return value;
        }

        public virtual int GetColumnIndex(string name)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));

            if (ColumnsIndices.TryGetValue(name, out int index))
                return index;

            return -1;
        }

        public string GetColumnName(int index)
        {
            var ptr = SQLiteDatabase._sqlite3_column_name16(CheckDisposed(), index);
            return ptr == IntPtr.Zero ? null : Marshal.PtrToStringUni(ptr);
        }

        public SQLiteColumnType GetColumnType(int index) => SQLiteDatabase._sqlite3_column_type(CheckDisposed(), index);

        public void StepAll() => Step((s, i) => true);
        public void StepOne() => Step((s, i) => false);
        public void StepMax(int maximumRows) => Step((s, i) => (i + 1) < maximumRows);
        public virtual void Step(Func<SQLiteStatement, int, bool> func)
        {
            if (func == null)
                throw new ArgumentNullException(nameof(func));

            int i = 0;
            var handle = CheckDisposed();
            do
            {
                SQLiteErrorCode code = SQLiteDatabase._sqlite3_step(handle);
                if (code == SQLiteErrorCode.SQLITE_DONE)
                {
                    Database.Log(TraceLevel.Verbose, "Step done at index " + i);
                    break;
                }

                if (code == SQLiteErrorCode.SQLITE_ROW)
                {
                    bool cont = func(this, i);
                    if (!cont)
                    {
                        Database.Log(TraceLevel.Verbose, "Step break at index " + i);
                        break;
                    }

                    i++;
                    continue;
                }
                Database.CheckError(code);
            }
            while (true);
        }

        public static string EscapeName(string name)
        {
            if (name == null)
                return null;

            return "\"" + name.Replace("\"", "\"\"") + "\"";
        }

        protected virtual void Dispose(bool disposing)
        {
            var handle = Interlocked.Exchange(ref _handle, IntPtr.Zero);
            if (handle != IntPtr.Zero)
            {
                SQLiteDatabase._sqlite3_finalize(handle);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~SQLiteStatement() => Dispose(false);
    }
}
