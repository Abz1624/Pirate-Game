var errors = UnityEditor.Compilation.CompilationPipeline.GetCompileMessages(
    UnityEditor.Compilation.CompilationPipeline.GetAssemblies()[0]);
return errors.Length;
