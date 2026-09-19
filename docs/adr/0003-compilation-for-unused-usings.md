# Unused usings use a C# compilation

`unused-using` needs a semantic model. We build an in-memory `CSharpCompilation` from scanned trees plus platform assemblies and read CS8019. Usings that do not resolve (missing package refs) are not reported, so we never delete a using that might be required.
