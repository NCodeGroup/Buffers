# 1031. A strict Base64url format decodes only the canonical unpadded text JOSE requires

- **Status:** Accepted
- **Date:** 2026-11-20
- **Deciders:** NCode Group

## Context

`BinaryTextFormat.Base64Url` decodes through `System.Buffers.Text.Base64Url`, which accepts `=` padding and whitespace,
so one byte string has several spellings. JOSE (RFC 7515 section 2, RFC 4648 section 5) allows one: the unpadded
URL-safe alphabet, with no whitespace and no non-zero unused bits in the final character. A JOSE library such as
NCode.Identity keeps its own guard in front of `RentDecoded` (NCodeGroup/identity#892). The BCL decoder already refuses
a length that leaves one character over and a final character with unused bits set, but not padding or whitespace.

## Decision

**`BinaryTextFormat.Base64UrlStrict` decodes only canonical unpadded Base64url: every character is one of `A`–`Z`,
`a`–`z`, `0`–`9`, `-` and `_`, and the text decodes with no character left over and no unused bit set; anything else
throws `FormatException`.**

- The value is appended to the enum as `4`; no existing value is renumbered.
- `RentDecoded` checks the alphabet with a `SearchValues<char>`, then requires `OperationStatus.Done` from
  `Base64Url.DecodeFromChars`.
- The check runs after the rental, so [ADR-1027](1027-text-base64-hex-and-random-helpers-rent-their-output-and-return-its-owner.md)'s
  rule applies: the rental is returned, zeroed when it is sensitive, before the exception leaves.
- `RentEncoded` writes the same text as `Base64Url`.
- `Base64Url` keeps its lenient decoding.

## Options considered

- Make `Base64Url` strict. Rejected: a validation change that consumers observe needs a major version.
- A `StrictBase64Url` class with `IsValid` and `TryDecode`. Rejected: it is a second shape beside ADR-1027's
  rent-and-return-the-owner helpers.
- A `strict` flag parameter. Rejected: a flag that selects behavior is against the C# conventions.
- A new `BinaryTextFormat` value (chosen). It is one more value of the vocabulary that both helpers already take.

## Consequences

- A JOSE caller passes `Base64UrlStrict` and drops its own guard.
- `Base64Url` text with padding or whitespace still decodes, so a caller that must refuse it chooses the strict format.
- Text encoded with either format decodes with both.

## References

- [`src/NCode.Buffers/BinaryTextFormat.cs`](../../src/NCode.Buffers/BinaryTextFormat.cs)
- [`src/NCode.Buffers/BinaryTextEncoding.cs`](../../src/NCode.Buffers/BinaryTextEncoding.cs)
- [ADR-1027](1027-text-base64-hex-and-random-helpers-rent-their-output-and-return-its-owner.md): text, Base64, hex and
  random helpers rent their output and return its owner.
- RFC 7515 section 2: <https://www.rfc-editor.org/rfc/rfc7515#section-2>
- RFC 4648 section 5: <https://www.rfc-editor.org/rfc/rfc4648#section-5>
- Issue: <https://github.com/NCodeGroup/Buffers/issues/123>
