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
/// <see cref="Dispose(bool)"/> returns the buffer to its pool, and disposing again does nothing. After it,
/// <see cref="CanRead"/>, <see cref="CanSeek"/> and <see cref="CanWrite"/> are <c>false</c>, <see cref="IsSensitive"/>
/// keeps its value, and <see cref="Flush"/> and <see cref="FlushAsync(CancellationToken)"/> still do nothing. The
/// members that read, write, seek or copy, and those that get or set the length or the position or get the written
/// bytes, throw <see cref="ObjectDisposedException"/>, except that <c>ReadAsync</c> and <c>WriteAsync</c> return a
/// canceled task when their token is canceled.
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
    /// <summary>
    /// Gets the size of the first buffer, and the least a growth rents.
    /// </summary>
    private int InitialCapacity { get; }

    /// <summary>
    /// Gets or sets the owner of the current buffer, or <see langword="null"/> before the first rental and after
    /// <see cref="Dispose(bool)"/>.
    /// </summary>
    private IMemoryOwner<byte>? Owner { get; set; }

    /// <summary>
    /// Gets or sets the number of bytes in the stream.
    /// </summary>
    private int DataLength { get; set; }

    /// <summary>
    /// Gets or sets the position, which may be past <see cref="DataLength"/>.
    /// </summary>
    private int CurrentPosition { get; set; }

    /// <summary>
    /// Gets or sets a value that indicates whether <see cref="Dispose(bool)"/> has run.
    /// </summary>
    private bool IsDisposed { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PooledMemoryStream"/> class.
    /// </summary>
    /// <param name="isSensitive">Whether the stream rents its buffers from the secure pool.</param>
    /// <param name="initialCapacity">
    /// The size of the first buffer, or 0 to use the default size.
    /// <c>BufferFactory.CreatePooledStream</c> checks the arguments.
    /// </param>
    internal PooledMemoryStream(bool isSensitive, int initialCapacity)
    {
        IsSensitive = isSensitive;
        InitialCapacity = initialCapacity == 0 ? BufferGrowth.DefaultCapacity : initialCapacity;
    }

    /// <summary>
    /// Gets the current buffer, or an empty memory when there is none.
    /// </summary>
    private Memory<byte> Buffer => Owner is null ? Memory<byte>.Empty : Owner.Memory;

    /// <summary>
    /// Gets the length of the current buffer, or 0 when the stream has none.
    /// </summary>
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
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            return Buffer[..DataLength];
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
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            return Buffer.Span[..DataLength];
        }
    }

    /// <inheritdoc />
    public override bool CanRead => !IsDisposed;

    /// <inheritdoc />
    public override bool CanSeek => !IsDisposed;

    /// <inheritdoc />
    public override bool CanWrite => !IsDisposed;

    /// <inheritdoc />
    /// <exception cref="ObjectDisposedException">The stream has been disposed.</exception>
    public override long Length
    {
        get
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            return DataLength;
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
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            return CurrentPosition;
        }
        set
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);

            if (value < 0 || value > Array.MaxLength)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "The position must be between 0 and the maximum length of an array."
                );
            }

            CurrentPosition = (int)value;
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
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        var target = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => CurrentPosition + offset,
            SeekOrigin.End => DataLength + offset,
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

        CurrentPosition = (int)target;
        return CurrentPosition;
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
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        if (value < 0 || value > Array.MaxLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "The length must be between 0 and the maximum length of an array."
            );
        }

        var newLength = (int)value;
        if (newLength > DataLength)
        {
            Grow(newLength)?.Dispose();
            Buffer.Span[DataLength..newLength].Clear();
        }
        else
        {
            var cut = Buffer.Span[newLength..DataLength];
            CryptographicOperationsExtensions.ZeroMemoryOrClear(cut);
        }

        DataLength = newLength;
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
        var span = buffer.AsSpan(offset, count);
        return Read(span);
    }

    /// <inheritdoc />
    /// <exception cref="ObjectDisposedException">The stream has been disposed.</exception>
    public override int Read(Span<byte> buffer)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        var count = Math.Min(buffer.Length, DataLength - CurrentPosition);
        if (count <= 0)
        {
            return 0;
        }

        Buffer.Span.Slice(CurrentPosition, count).CopyTo(buffer);
        CurrentPosition += count;
        return count;
    }

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count)
    {
        ValidateBufferArguments(buffer, offset, count);
        var span = buffer.AsSpan(offset, count);
        Write(span);
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
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        var end = (long)CurrentPosition + buffer.Length;
        if (end > Array.MaxLength)
        {
            throw new IOException("Stream was too long.");
        }

        var previous = Grow((int)end);

        if (CurrentPosition > DataLength)
        {
            Buffer.Span[DataLength..CurrentPosition].Clear();
        }

        var target = Buffer.Span[CurrentPosition..];
        buffer.CopyTo(target);
        previous?.Dispose();

        CurrentPosition = (int)end;
        if (CurrentPosition > DataLength)
        {
            DataLength = CurrentPosition;
        }
    }

    /// <inheritdoc />
    /// <exception cref="ObjectDisposedException">The stream has been disposed.</exception>
    public override int ReadByte()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        if (CurrentPosition >= DataLength)
        {
            return -1;
        }

        return Buffer.Span[CurrentPosition++];
    }

    /// <inheritdoc />
    /// <exception cref="ObjectDisposedException">The stream has been disposed.</exception>
    /// <exception cref="IOException">The write would end past <see cref="Array.MaxLength"/>.</exception>
    public override void WriteByte(byte value)
    {
        var span = new ReadOnlySpan<byte>(in value);
        Write(span);
    }

#pragma warning disable RS0027 // Stream declares these overloads with a default token; the overrides keep it, as MemoryStream's do.
    /// <inheritdoc />
    /// <remarks>
    /// This method completes synchronously, and returns a canceled task when <paramref name="cancellationToken"/> is
    /// canceled. An exception is thrown synchronously instead of being returned in the task.
    /// </remarks>
    public override ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled<int>(cancellationToken);
        }

        var read = Read(buffer.Span);
        return new ValueTask<int>(read);
    }
#pragma warning restore RS0027

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

        var read = Read(buffer, offset, count);
        return Task.FromResult(read);
    }

#pragma warning disable RS0027 // Stream declares these overloads with a default token; the overrides keep it, as MemoryStream's do.
    /// <inheritdoc />
    /// <remarks>
    /// This method completes synchronously, and returns a canceled task when <paramref name="cancellationToken"/> is
    /// canceled. An exception is thrown synchronously instead of being returned in the task.
    /// </remarks>
    public override ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled(cancellationToken);
        }

        Write(buffer.Span);
        return ValueTask.CompletedTask;
    }
#pragma warning restore RS0027

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
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        if (CurrentPosition >= DataLength)
        {
            return;
        }

        var remaining = Buffer.Span[CurrentPosition..DataLength];
        destination.Write(remaining);
        CurrentPosition = DataLength;
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
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellationToken);
        }

        if (CurrentPosition >= DataLength)
        {
            return Task.CompletedTask;
        }

        var start = CurrentPosition;
        CurrentPosition = DataLength;
        var remaining = Buffer[start..DataLength];
        var write = destination.WriteAsync(remaining, cancellationToken);
        return write.AsTask();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            IsDisposed = true;
            var owner = Owner;
            Owner = null;
            DataLength = 0;
            CurrentPosition = 0;
            owner?.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// Rents a buffer of at least <paramref name="required"/> bytes when the current one is smaller, copies the
    /// stream's bytes into it and makes it current.
    /// </summary>
    /// <param name="required">The number of bytes the buffer must hold.</param>
    /// <returns>
    /// The previous owner, which the caller disposes once it no longer reads the old buffer, or
    /// <see langword="null"/> when the buffer did not grow.
    /// </returns>
    private IMemoryOwner<byte>? Grow(int required)
    {
        if (required <= Capacity)
        {
            return null;
        }

        var newSize = BufferGrowth.GetNewSize(required, Capacity, InitialCapacity);
        var pool = BufferFactory.ChoosePool<byte>(IsSensitive);
        var owner = pool.Rent(newSize);
        Buffer.Span[..DataLength].CopyTo(owner.Memory.Span);

        var previous = Owner;
        Owner = owner;
        return previous;
    }
}
