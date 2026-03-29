using Microsoft.Extensions.Logging.Abstractions;
using SanitizeService.Domain;
using SanitizeService.Domain.Exceptions;

namespace SanitizeService.Tests;

public sealed class AbcFileSanitizerTests
{
    private readonly AbcFileSanitizer _sut = new(NullLogger<AbcFileSanitizer>.Instance);

    private static MemoryStream ToSeekableStream(ReadOnlySpan<byte> data)
    {
        var ms = new MemoryStream(data.Length);
        ms.Write(data);
        ms.Position = 0;
        return ms;
    }

    [Fact]
    public async Task Valid_blocks_round_trip()
    {
        var input = ToSeekableStream("123A1CA2CA9C789"u8);
        await using var output = await _sut.SanitizeAsync(input, CancellationToken.None);
        var buf = await ReadAllAsync(output);
        Assert.Equal("123A1CA2CA9C789"u8.ToArray(), buf);
    }

    [Fact]
    public async Task Empty_middle_is_valid()
    {
        var input = ToSeekableStream("123789"u8);
        await using var output = await _sut.SanitizeAsync(input, CancellationToken.None);
        var buf = await ReadAllAsync(output);
        Assert.Equal("123789"u8.ToArray(), buf);
    }

    [Fact]
    public async Task Long_concatenated_valid_blocks_round_trip()
    {
        // Middle is a long run of valid 3-byte blocks (A1C A3C A7C A2C A5C), no newline bytes.
        var input = ToSeekableStream("123A1CA3CA7CA2CA5C789"u8);
        await using var output = await _sut.SanitizeAsync(input, CancellationToken.None);
        var buf = await ReadAllAsync(output);
        Assert.Equal("123A1CA3CA7CA2CA5C789"u8.ToArray(), buf);
    }

    [Fact]
    public async Task Block_with_letter_middle_byte_F_replaced_with_A255C()
    {
        // Block AFC: middle byte is F (not 1–9) → entire block replaced with A255C.
        var input = ToSeekableStream("123A1CA3CAFC789"u8);
        await using var output = await _sut.SanitizeAsync(input, CancellationToken.None);
        var buf = await ReadAllAsync(output);
        Assert.Equal("123A1CA3CA255C789"u8.ToArray(), buf);
    }

    [Fact]
    public async Task Invalid_digit_in_block_is_replaced_with_A255C()
    {
        var input = ToSeekableStream("123A0C789"u8);
        await using var output = await _sut.SanitizeAsync(input, CancellationToken.None);
        var buf = await ReadAllAsync(output);
        Assert.Equal("123A255C789"u8.ToArray(), buf);
    }

    [Fact]
    public async Task Incomplete_middle_not_multiple_of_three_bytes_throws()
    {
        // Middle is 4 bytes (XA1C): not a whole number of 3-byte blocks.
        var input = ToSeekableStream("123XA1C789"u8);
        var ex = await Assert.ThrowsAsync<InvalidAbcStructureException>(async () =>
            await _sut.SanitizeAsync(input, CancellationToken.None));
        Assert.Contains("3-byte", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Incomplete_middle_two_bytes_throws()
    {
        var input = ToSeekableStream("123AB789"u8);
        var ex = await Assert.ThrowsAsync<InvalidAbcStructureException>(async () =>
            await _sut.SanitizeAsync(input, CancellationToken.None));
        Assert.Contains("3-byte", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Invalid_body_length_not_divisible_by_three_throws()
    {
        // totalLength 14 → body 8 bytes (not a multiple of 3).
        var input = ToSeekableStream("123XXXXXXXX789"u8);
        await Assert.ThrowsAsync<InvalidAbcStructureException>(async () =>
            await _sut.SanitizeAsync(input, CancellationToken.None));
    }

    [Fact]
    public async Task Too_short_throws()
    {
        var input = ToSeekableStream("12"u8);
        var ex = await Assert.ThrowsAsync<InvalidAbcStructureException>(async () =>
            await _sut.SanitizeAsync(input, CancellationToken.None));
        Assert.Contains("at least 6 bytes", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Wrong_header_throws()
    {
        var input = ToSeekableStream("999A1C789"u8);
        await Assert.ThrowsAsync<InvalidAbcStructureException>(async () =>
            await _sut.SanitizeAsync(input, CancellationToken.None));
    }

    [Fact]
    public async Task Wrong_footer_throws()
    {
        var input = ToSeekableStream("123A1C788"u8);
        await Assert.ThrowsAsync<InvalidAbcStructureException>(async () =>
            await _sut.SanitizeAsync(input, CancellationToken.None));
    }

    [Fact]
    public async Task Non_seekable_stream_is_accepted_when_content_is_valid_abc()
    {
        var inner = ToSeekableStream("123A1C789"u8);
        await using var nonSeek = new NonSeekableStreamWrapper(inner);
        await using var output = await _sut.SanitizeAsync(nonSeek, CancellationToken.None);
        var buf = await ReadAllAsync(output);
        Assert.Equal("123A1C789"u8.ToArray(), buf);
    }

    [Fact]
    public async Task Byte_sequence_789_in_body_is_sanitized_as_blocks_not_as_footer()
    {
        // Literal "789" appears as a middle 3-byte block (invalid → A255C); real footer is only the final 3 bytes.
        var input = ToSeekableStream("123A1C789A2C789"u8);
        await using var output = await _sut.SanitizeAsync(input, CancellationToken.None);
        var buf = await ReadAllAsync(output);
        Assert.Equal("123A1CA255CA2C789"u8.ToArray(), buf);
    }

    private static async Task<byte[]> ReadAllAsync(Stream s)
    {
        s.Position = 0;
        var ms = new MemoryStream();
        await s.CopyToAsync(ms);
        return ms.ToArray();
    }

    private sealed class NonSeekableStreamWrapper : Stream
    {
        private readonly Stream _inner;

        public NonSeekableStreamWrapper(Stream inner)
        {
            _inner = inner;
        }

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default) =>
            _inner.ReadAsync(buffer, cancellationToken);

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
