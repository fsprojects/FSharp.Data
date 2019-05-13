module FSharp.Data.Tests.MockStreams

open System.IO

/// Checks that all reads are asynchronous.
type AsyncTextReader(underlying: TextReader) =
    inherit TextReader()
    override this.Read() = failwithf "Synchronous call to Read()"
    override this.ReadBlock(_,_,_) = failwithf "Synchronous call to ReadBlock()"
    override this.ReadLine() = failwithf "Synchronous call to ReadLine()"
    override this.ReadToEnd() = failwithf "Synchronous call to ReadToEnd()"
    override this.ReadAsync(buf, start, len) = underlying.ReadAsync(buf, start, len)
    override this.ReadBlockAsync(buf, start, len) = underlying.ReadBlockAsync(buf, start, len)
    override this.ReadLineAsync() = underlying.ReadLineAsync()
    override this.ReadToEndAsync() = underlying.ReadToEndAsync()
    new(s: Stream) = new AsyncTextReader(new StreamReader(s))

/// Checks that all writes are asynchronous.
type AsyncTextWriter(underlying: TextWriter) =
    inherit TextWriter()
    override this.Encoding = underlying.Encoding
    override this.Write(c: char) : unit = failwith "Synchronous call to Write"
    override this.WriteAsync(s: string) = underlying.WriteAsync(s)
    override this.WriteAsync(c: char) = underlying.WriteAsync(c)
    override this.WriteLineAsync() = underlying.WriteLineAsync()
    override this.WriteLineAsync(s: string) = underlying.WriteLineAsync(s)
    override this.WriteLineAsync(c: char) = underlying.WriteLineAsync(c)
    new(s: Stream) = new AsyncTextWriter(new StreamWriter(s, AutoFlush = true))
