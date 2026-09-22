var sb = new System.Text.StringBuilder();
foreach (var asm in UnityEditor.Compilation.CompilationPipeline.GetAssemblies())
{
    if (asm.name != "HighSeas.Tests.EditMode") continue;
    var msgs = UnityEditor.Compilation.CompilationPipeline.GetCompileMessages(asm.name);
    for (var i = 0; i < msgs.Length; i++)
    {
        if (msgs[i].type == UnityEditor.Compilation.CompilerMessageType.Error)
            sb.AppendLine(msgs[i].message);
    }
}
return sb.Length == 0 ? "no_errors" : sb.ToString();
