# Third-party notices

Snapp! Shop's read-only request protocol was researched using the public source at
https://github.com/sepehr071/snappshop-mcp. The recorded test fixtures
`tests/fixtures/product.json` and `tests/fixtures/search.json` originate from that
repository and are provided under its MIT license below. The upstream fixture
contains an abbreviated vendor list; our tests explicitly verify rejection of
that partial response. Our collector is independently implemented and does not
apply the MCP tool's 25-offer display cap.

MIT License

Copyright (c) 2026 Sepehr Radmard

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

The Vazirmatn font is by the Vazirmatn project / Saber Rastikerdar, licensed under
SIL Open Font License 1.1. The font is bundled locally for Persian text rendering.
Its license is in `frontend/public/fonts/OFL.txt`.

This project is independent of, and is not endorsed by, Snapp! Shop. The source
adapter is a guest, read-only integration. Its upstream API is not a documented
service contract and may change.
