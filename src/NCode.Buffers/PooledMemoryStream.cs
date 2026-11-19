// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using JetBrains.Annotations;

namespace NCode.Buffers;

/// <summary>
/// A seekable, readable and writable <see cref="Stream"/> over one pooled buffer, which keeps everything written in
/// one contiguous span so <see cref="WrittenSpan"/> and <see cref="WrittenMemory"/> are always available.
/// </summary>
/// <remarks>
/// <para>
/// Create an instance with <see cref="BufferFactory.CreatePooledStream"/>. The stream rents its buffer lazily, on the
/// first write or <see cref="SetLength"/> that needs room, from the pool that <see cref="IsSensitive"/> selects. The
/// buffer holds at most <see cref="Array.MaxLength"/> bytes.
/// </para>
/// <para>
/// When the buffer is too small, the stream rents a larger one, copies the existing bytes into it, and disposes the
/// old one. When the stream is sensitive, the old buffer is zeroed. A <see cref="Span{T}"/> or <see cref="Memory{T}"/>
/// that the stream returned from <see cref="WrittenSpan"/> or <see cref="WrittenMemory"/> is therefore not valid after
/// the next write or call to <see cref="SetLength"/>. Writing the stream's own <see cref="WrittenSpan"/> into the
/// stream is supported.
/// </para>
/// <para>
/// Writing past the end of the stream zero-fills the gap first, and <see cref="SetLength"/> zeroes the bytes it cuts
/// off. A write that would end past <see cref="Array.MaxLength"/> throws <see cref="IOException"/>.
/// </para>
/// <para>
/// <see cref="Dispose(bool)"/> returns the buffer to its pool. After it, <see cref="CanRead"/>, <see cref="CanSeek"/>
/// and <see cref="CanWrite"/> are <c>false</c>, and every other member except <see cref="IsSensitive"/> throws
/// <see cref="ObjectDisposedException"/>.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// using var stream = BufferFactory.CreatePooledStream(isSensitive: true);
///
/// // Write some data
/// stream.Write("secret"u8);
///
/// // Rewind and read it back
/// stream.Position = 0;
/// Span&lt;byte&gt; buffer = stackalloc byte[6];
/// int count = stream.Read(buffer);
/// </code>
/// </example>
[PublicAPI]
public sealed class PooledMemoryStream : Stream
{
    private const int DefaultInitialCapacity = 256;

    private readonly int _initialCapacity;
    private IMemoryOwner<byte>? _owner;
    private int _length;
    private int _position;
    private bool _disposed;

    internal PooledMemoryStream(bool isSensitive, int initialCapacity)
    {
        IsSensitive = isSensitive;
        _initialCapacity = initialCapacity == 0 ? DefaultInitialCapacity : initialCapacity;
    }

    private Memory<byte> Buffer => _owner is null ? Memory<byte>.Empty : _owner.Memory;

    internal int Capacity => Buffer.Length;

    /// <summary>
    /// Gets a value that indicates whether the stream rents its buffers from the secure pool, which zeroes them when
    /// they are disposed.
    /// </summary>
    /// <value><c>true</c> when the buffers hold sensitive data; otherwise, <c>false</c>.</value>
    public bool IsSensitive { get; }

    /// <summary>
    /// Gets a <see cref="ReadOnlyMemory{T}"/> of the bytes from the start of the stream up to <see cref="Length"/>.
    /// </summary>
    /// <value>A read-only memory containing all bytes of the stream.</value>
    /// <exception cref="ObjectDisposedException">The stream has been disposed.</exception>
    /// <remarks>
    /// The returned memory is not valid after the next write or call to <see cref="SetLength"/>, because the buffer
    /// may grow and the old buffer is then zeroed when the stream is sensitive.
    /// </remarks>
    public ReadOnlyMemory<byte> WrittenMemory
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return Buffer[.._length];
        }
    }

    /// <summary>
    /// Gets a <see cref="ReadOnlySpan{T}"/> of the bytes from the start of the stream up to <see cref="Length"/>.
    /// </summary>
    /// <value>A read-only span containing all bytes of the stream.</value>
    /// <exception cref="ObjectDisposedException">The stream has been disposed.</exception>
    /// <remarks>
    /// The returned span is not valid after the next write or call to <see cref="SetLength"/>, because the buffer
    /// may grow and the old buffer is then zeroed when the stream is sensitive.
    /// </remarks>
    public ReadOnlySpan<byte> WrittenSpan
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return Buffer.Span[.._length];
        }
    }

    /// <inheritdoc />
    public override bool CanRead => !_disposed;

    /// <inheritdoc />
    public override bool CanSeek => !_disposed;

    /// <inheritdoc />
    public override bool CanWrite => !_disposed;

    /// <inheritdoc />
    /// <exception cref="ObjectDisposedException">The stream has been disposed.</exception>
    public override long Length
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _length;
        }
    }

    /// <inheritdoc />
    /// <exception cref="ObjectDisposedException">The stream has been disposed.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The value is negative or greater than <see cref="Array.MaxLength"/>.
    /// </exception>
    /// <remarks>
    /// The position may be set past <see cref="Length"/>. A read there returns 0, and a write there zero-fills the gap.
    /// </remarks>
    public override long Position
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _position;
        }
        set
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (value < 0 || value > Array.MaxLength)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "The position must be between 0 and the maximum length of an array."
                );
            }

            _position = (int)value;
        }
    }

    /// <inheritdoc />
    /// <exception cref="ObjectDisposedException">The stream has been disposed.</exception>
    /// <exception cref="ArgumentException"><paramref name="origin"/> is not a <see cref="SeekOrigin"/> value.</exception>
    /// <exception cref="IOException">The new position would be before the beginning of the stream.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The new position would be greater than <see cref="Array.MaxLength"/>.
    /// </exception>
    public override long Seek(long offset, SeekOrigin origin)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var target = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => _length + offset,
            _ => throw new ArgumentException(
                "The origin is not a SeekOrigin value.",
                nameof(origin)
            ),
        };

        if (target < 0)
        {
            throw new IOException(
                "An attempt was made to move the position before the beginning of the stream."
            );
        }

        if (target > Array.MaxLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(offset),
                offset,
                "The new position must not be greater than the maximum length of an array."
            );
        }

        _position = (int)target;
        return _position;
    }

    /// <inheritdoc />
    /// <exception cref="ObjectDisposedException">The stream has been disposed.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="value"/> is negative or greater than <see cref="Array.MaxLength"/>.
    /// </exception>
    /// <remarks>
    /// When the stream grows, the new bytes are zero. When it shrinks, the bytes that are cut off are zeroed. The
    /// position does not change.
    /// </remarks>
    public override void SetLength(long value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (value < 0 || value > Array.MaxLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "The length must be between 0 and the maximum length of an array."
            );
        }

        var newLength = (int)value;
        if (newLength > _length)
        {
            Grow(newLength)?.Dispose();
            Buffer.Span[_length..newLength].Clear();
        }
        else
        {
            CryptographicOperationsExtensions.ZeroMemoryOrClear(Buffer.Span[newLength.._length]);
        }

        _length = newLength;
    }

    /// <inheritdoc />
    /// <remarks>
    /// This method does nothing, because the stream has no backing store to flush to.
    /// </remarks>
    public override void Flush() { }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count)
    {
        ValidateBufferArguments(buffer, offset, count);
        return Read(buffer.AsSpan(offset, count));
    }

    /// <inheritdoc />
    /// <exception cref="ObjectDisposedException">The stream has been disposed.</exception>
    public override int Read(Span<byte> buffer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var count = Math.Min(buffer.Length, _length - _position);
        if (count <= 0)
        {
            return 0;
        }

        Buffer.Span.Slice(_position, count).CopyTo(buffer);
        _position += count;
        return count;
    }

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count)
    {
        ValidateBufferArguments(buffer, offset, count);
        Write(buffer.AsSpan(offset, count));
    }

    /// <inheritdoc />
    /// <exception cref="ObjectDisposedException">The stream has been disposed.</exception>
    /// <exception cref="IOException">The write would end past <see cref="Array.MaxLength"/>.</exception>
    /// <remarks>
    /// When the write starts past <see cref="Length"/>, the gap is zero-filled first. When the buffer must grow, the
    /// input is copied before the old buffer is disposed, so the input may be the stream's own
    /// <see cref="WrittenSpan"/>.
    /// </remarks>
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var end = (long)_position + buffer.Length;
        if (end > Array.MaxLength)
        {
            throw new IOException("Stream was too long.");
        }

        var previous = Grow((int)end);

        if (_position > _length)
        {
            Buffer.Span[_length.._position].Clear();
        }

        buffer.CopyTo(Buffer.Span[_position..]);
        previous?.Dispose();

        _position = (int)end;
        if (_position > _length)
        {
            _length = _position;
        }
    }

    /// <inheritdoc />
    /// <exception cref="ObjectDisposedException">The stream has been disposed.</exception>
    public override int ReadByte()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_position >= _length)
        {
            return -1;
        }

        return Buffer.Span[_position++];
    }

    /// <inheritdoc />
    /// <exception cref="ObjectDisposedException">The stream has been disposed.</exception>
    /// <exception cref="IOException">The write would end past <see cref="Array.MaxLength"/>.</exception>
    public override void WriteByte(byte value) => Write(new ReadOnlySpan<byte>(in value));

    /// <inheritdoc />
    /// <remarks>
    /// This method completes synchronously, and returns a canceled task when <paramref name="cancellationToken"/> is
    /// canceled. An exception is thrown synchronously instead of being returned in the task.
    /// </remarks>
    public override ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken
    )
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled<int>(cancellationToken);
        }

        return new ValueTask<int>(Read(buffer.Span));
    }

    /// <inheritdoc />
    /// <remarks>
    /// This method completes synchronously, and returns a canceled task when <paramref name="cancellationToken"/> is
    /// canceled. An exception is thrown synchronously instead of being returned in the task.
    /// </remarks>
    public override Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<int>(cancellationToken);
        }

        return Task.FromResult(Read(buffer, offset, count));
    }

    /// <inheritdoc />
    /// <remarks>
    /// This method completes synchronously, and returns a canceled task when <paramref name="cancellationToken"/> is
    /// canceled. An exception is thrown synchronously instead of being returned in the task.
    /// </remarks>
    public override ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken
    )
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled(cancellationToken);
        }

        Write(buffer.Span);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    /// <remarks>
    /// This method completes synchronously, and returns a canceled task when <paramref name="cancellationToken"/> is
    /// canceled. An exception is thrown synchronously instead of being returned in the task.
    /// </remarks>
    public override Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellationToken);
        }

        Write(buffer, offset, count);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    /// <remarks>
    /// This method does nothing and completes synchronously, or returns a canceled task when
    /// <paramref name="cancellationToken"/> is canceled.
    /// </remarks>
    public override Task FlushAsync(CancellationToken cancellationToken) =>
        cancellationToken.IsCancellationRequested
            ? Task.FromCanceled(cancellationToken)
            : Task.CompletedTask;

    /// <inheritdoc />
    /// <exception cref="ObjectDisposedException">The stream has been disposed.</exception>
    /// <remarks>
    /// This method copies the bytes from <see cref="Position"/> up to <see cref="Length"/> straight from the pooled
    /// buffer to <paramref name="destination"/>, without an intermediate array, and moves <see cref="Position"/> to
    /// <see cref="Length"/>. When <see cref="Position"/> is at or past <see cref="Length"/>, nothing is copied and
    /// <see cref="Position"/> does not change. The <paramref name="bufferSize"/> is only validated.
    /// </remarks>
    public override void CopyTo(Stream destination, int bufferSize)
    {
        ValidateCopyToArguments(destination, bufferSize);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_position >= _length)
        {
            return;
        }

        destination.Write(Buffer.Span[_position.._length]);
        _position = _length;
    }

    /// <inheritdoc />
    /// <exception cref="ObjectDisposedException">The stream has been disposed.</exception>
    /// <remarks>
    /// <para>
    /// This method copies the bytes from <see cref="Position"/> up to <see cref="Length"/> straight from the pooled
    /// buffer to <paramref name="destination"/>, without an intermediate array. It validates its arguments, checks
    /// the disposed state and the token synchronously, and moves <see cref="Position"/> to <see cref="Length"/>
    /// before it starts writing. When <see cref="Position"/> is at or past <see cref="Length"/>, nothing is copied and
    /// <see cref="Position"/> does not change. The <paramref name="bufferSize"/> is only validated.
    /// </para>
    /// <para>
    /// The stream must not be written, shrunk or disposed until the returned task completes. A failure of the
    /// destination is returned in the task.
    /// </para>
    /// </remarks>
    public override Task CopyToAsync(
        Stream destination,
        int bufferSize,
        CancellationToken cancellationToken
    )
    {
        ValidateCopyToArguments(destination, bufferSize);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellationToken);
        }

        if (_position >= _length)
        {
            return Task.CompletedTask;
        }

        var start = _position;
        _position = _length;
        return destination.WriteAsync(Buffer[start.._length], cancellationToken).AsTask();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            _disposed = true;
            var owner = _owner;
            _owner = null;
            _length = 0;
            _position = 0;
            owner?.Dispose();
        }

        base.Dispose(disposing);
    }

    private IMemoryOwner<byte>? Grow(int required)
    {
        if (required <= Capacity)
        {
            return null;
        }

        var doubled = (int)Math.Min(2L * Capacity, Array.MaxLength);
        var newSize = Math.Max(required, Math.Max(_initialCapacity, doubled));

        var owner = BufferFactory.ChoosePool<byte>(IsSensitive).Rent(newSize);
        Buffer.Span[.._length].CopyTo(owner.Memory.Span);

        var previous = _owner;
        _owner = owner;
        return previous;
    }
}
