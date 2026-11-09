// The stripped interop assembly exports NullableAttribute without usable
// constructors. Match the existing mods' internal compiler metadata shim.
namespace System.Runtime.CompilerServices;

[AttributeUsage(AttributeTargets.All)]
internal sealed class NullableAttribute : Attribute
{
    public NullableAttribute(byte value) { }
    public NullableAttribute(byte[] values) { }
}
