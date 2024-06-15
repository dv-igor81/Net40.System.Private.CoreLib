// Type: System.Runtime.CompilerServices.Unsafe 
// Assembly: System.Runtime.CompilerServices.Unsafe, Version=4.0.6.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a

.class public abstract sealed auto ansi beforefieldinit
  System.Runtime.CompilerServices.Unsafe
    extends System.Object
{

  .method public hidebysig static !!0/*T*/
    Read<T>(
      void* source
    ) cil managed
  {
    .custom instance void System.Runtime.Versioning.NonVersionableAttribute::.ctor()
      = (01 00 00 00 )
    .maxstack 8

    // [16 53 - 16 65]
    IL_0000: ldarg.0      // source
    IL_0001: ldobj        !!0/*T*/
    IL_0006: ret

  } // end of method Unsafe::Read

  .method public hidebysig static !!0/*T*/
    ReadUnaligned<T>(
      void* source
    ) cil managed
  {
    .custom instance void System.Runtime.Versioning.NonVersionableAttribute::.ctor()
      = (01 00 00 00 )
    .maxstack 8

    // [20 62 - 20 74]
    IL_0000: ldarg.0      // source
    IL_0001: unaligned.   1 // 0x01
    IL_0004: ldobj        !!0/*T*/
    IL_0009: ret

  } // end of method Unsafe::ReadUnaligned

  .method public hidebysig static !!0/*T*/
    ReadUnaligned<T>(
      unsigned int8& source
    ) cil managed
  {
    .custom instance void System.Runtime.Versioning.NonVersionableAttribute::.ctor()
      = (01 00 00 00 )
    .maxstack 8

    // [24 58 - 24 74]
    IL_0000: ldarg.0      // source
    IL_0001: unaligned.   1 // 0x01
    IL_0004: ldobj        !!0/*T*/
    IL_0009: ret

  } // end of method Unsafe::ReadUnaligned

  .method public hidebysig static void
    Write<T>(
      void* destination,
      !!0/*T*/ 'value'
    ) cil managed
  {
    .custom instance void System.Runtime.Versioning.NonVersionableAttribute::.ctor()
      = (01 00 00 00 )
    .maxstack 8

    // [28 71 - 28 96]
    IL_0000: ldarg.0      // destination
    IL_0001: ldarg.1      // 'value'
    IL_0002: stobj        !!0/*T*/
    IL_0007: ret

  } // end of method Unsafe::Write

  .method public hidebysig static void
    WriteUnaligned<T>(
      void* destination,
      !!0/*T*/ 'value'
    ) cil managed
  {
    .custom instance void System.Runtime.Versioning.NonVersionableAttribute::.ctor()
      = (01 00 00 00 )
    .maxstack 8

    // [34 7 - 34 32]
    IL_0000: ldarg.0      // destination
    IL_0001: ldarg.1      // 'value'
    IL_0002: unaligned.   1 // 0x01
    IL_0005: stobj        !!0/*T*/
    IL_000a: ret

  } // end of method Unsafe::WriteUnaligned

  .method public hidebysig static void
    WriteUnaligned<T>(
      unsigned int8& destination,
      !!0/*T*/ 'value'
    ) cil managed
  {
    .custom instance void System.Runtime.Versioning.NonVersionableAttribute::.ctor()
      = (01 00 00 00 )
    .maxstack 8

    // [43 7 - 43 36]
    IL_0000: ldarg.0      // destination
    IL_0001: ldarg.1      // 'value'
    IL_0002: unaligned.   1 // 0x01
    IL_0005: stobj        !!0/*T*/
    IL_000a: ret

  } // end of method Unsafe::WriteUnaligned

  .method public hidebysig static void
    Copy<T>(
      void* destination,
      !!0/*T*/& source
    ) cil managed
  {
    .custom instance void System.Runtime.Versioning.NonVersionableAttribute::.ctor()
      = (01 00 00 00 )
    .maxstack 8

    // [50 7 - 50 33]
    IL_0000: ldarg.0      // destination
    IL_0001: ldarg.1      // source
    IL_0002: ldobj        !!0/*T*/
    IL_0007: stobj        !!0/*T*/
    IL_000c: ret

  } // end of method Unsafe::Copy

  .method public hidebysig static void
    Copy<T>(
      !!0/*T*/& destination,
      void* source
    ) cil managed
  {
    .custom instance void System.Runtime.Versioning.NonVersionableAttribute::.ctor()
      = (01 00 00 00 )
    .maxstack 8

    // [57 7 - 57 33]
    IL_0000: ldarg.0      // destination
    IL_0001: ldarg.1      // source
    IL_0002: ldobj        !!0/*T*/
    IL_0007: stobj        !!0/*T*/
    IL_000c: ret

  } // end of method Unsafe::Copy

  .method public hidebysig static void*
    AsPointer<T>(
      !!0/*T*/& 'value'
    ) cil managed
  {
    .custom instance void System.Runtime.Versioning.NonVersionableAttribute::.ctor()
      = (01 00 00 00 )
    .maxstack 8

    // [62 61 - 62 78]
    IL_0000: ldarg.0      // 'value'
    IL_0001: conv.u
    IL_0002: ret

  } // end of method Unsafe::AsPointer

  .method public hidebysig static int32
    SizeOf<T>() cil managed
  {
    .custom instance void System.Runtime.Versioning.NonVersionableAttribute::.ctor()
      = (01 00 00 00 )
    .maxstack 8

    // [66 38 - 66 48]
    IL_0000: sizeof       !!0/*T*/
    IL_0006: ret

  } // end of method Unsafe::SizeOf

  .method public hidebysig static void
    CopyBlock(
      void* destination,
      void* source,
      unsigned int32 byteCount
    ) cil managed
  {
    .custom instance void System.Runtime.Versioning.NonVersionableAttribute::.ctor()
      = (01 00 00 00 )
    .maxstack 8

    // [73 7 - 73 72]
    IL_0000: ldarg.0      // destination
    IL_0001: ldarg.1      // source
    IL_0002: ldarg.2      // byteCount
    IL_0003: cpblk
    IL_0005: ret

  } // end of method Unsafe::CopyBlock

  .method public hidebysig static void
    CopyBlock(
      unsigned int8& destination,
      unsigned int8& source,
      unsigned int32 byteCount
    ) cil managed
  {
    .custom instance void System.Runtime.Versioning.NonVersionableAttribute::.ctor()
      = (01 00 00 00 )
    .maxstack 8

    // [81 7 - 81 62]
    IL_0000: ldarg.0      // destination
    IL_0001: ldarg.1      // source
    IL_0002: ldarg.2      // byteCount
    IL_0003: cpblk
    IL_0005: ret

  } // end of method Unsafe::CopyBlock

  .method public hidebysig static void
    CopyBlockUnaligned(
      void* destination,
      void* source,
      unsigned int32 byteCount
    ) cil managed
  {
    .custom instance void System.Runtime.Versioning.NonVersionableAttribute::.ctor()
      = (01 00 00 00 )
    .maxstack 8

    // [89 7 - 89 72]
    IL_0000: ldarg.0      // destination
    IL_0001: ldarg.1      // source
    IL_0002: ldarg.2      // byteCount
    IL_0003: unaligned.   1 // 0x01
    IL_0006: cpblk
    IL_0008: ret

  } // end of method Unsafe::CopyBlockUnaligned

  .method public hidebysig static void
    CopyBlockUnaligned(
      unsigned int8& destination,
      unsigned int8& source,
      unsigned int32 byteCount
    ) cil managed
  {
    .custom instance void System.Runtime.Versioning.NonVersionableAttribute::.ctor()
      = (01 00 00 00 )
    .maxstack 8

    // [97 7 - 97 62]
    IL_0000: ldarg.0      // destination
    IL_0001: ldarg.1      // source
    IL_0002: ldarg.2      // byteCount
    IL_0003: unaligned.   1 // 0x01
    IL_0006: cpblk
    IL_0008: ret

  } // end of method Unsafe::CopyBlockUnaligned

  .method public hidebysig static void
    InitBlock(
      void* startAddress,
      unsigned int8 'value',
      unsigned int32 byteCount
    ) cil managed
  {
    .custom instance void System.Runtime.Versioning.NonVersionableAttribute::.ctor()
      = (01 00 00 00 )
    .maxstack 8

    // [105 7 - 105 69]
    IL_0000: ldarg.0      // startAddress
    IL_0001: ldarg.1      // 'value'
    IL_0002: ldarg.2      // byteCount
    IL_0003: initblk
    IL_0005: ret

  } // end of method Unsafe::InitBlock

  .method public hidebysig static void
    InitBlock(
      unsigned int8& startAddress,
      unsigned int8 'value',
      unsigned int32 byteCount
    ) cil managed
  {
    .custom instance void System.Runtime.Versioning.NonVersionableAttribute::.ctor()
      = (01 00 00 00 )
    .maxstack 8

    // [113 7 - 113 64]
    IL_0000: ldarg.0      // startAddress
    IL_0001: ldarg.1      // 'value'
    IL_0002: ldarg.2      // byteCount
    IL_0003: initblk
    IL_0005: ret

  } // end of method Unsafe::InitBlock

  .method public hidebysig static void
    InitBlockUnaligned(
      void* startAddress,
      unsigned int8 'value',
      unsigned int32 byteCount
    ) cil managed
  {
    .custom instance void System.Runtime.Versioning.NonVersionableAttribute::.ctor()
      = (01 00 00 00 )
    .maxstack 8

    // [121 7 - 121 69]
    IL_0000: ldarg.0      // startAddress
    IL_0001: ldarg.1      // 'value'
    IL_0002: ldarg.2      // byteCount
    IL_0003: unaligned.   1 // 0x01
    IL_0006: initblk
    IL_0008: ret

  } // end of method Unsafe::InitBlockUnaligned

  .method public hidebysig static void
    InitBlockUnaligned(
      unsigned int8& startAddress,
      unsigned int8 'value',
      unsigned int32 byteCount
    ) cil managed
  {
    .custom instance void System.Runtime.Versioning.NonVersionableAttribute::.ctor()
      = (01 00 00 00 )
    .maxstack 8

    // [129 7 - 129 64]
    IL_0000: ldarg.0      // startAddress
    IL_0001: ldarg.1      // 'value'
    IL_0002: ldarg.2      // byteCount
    IL_0003: unaligned.   1 // 0x01
    IL_0006: initblk
    IL_0008: ret

  } // end of method Unsafe::InitBlockUnaligned

  .method public hidebysig static !!0/*T*/
    As<class T>(
      object o
    ) cil managed
  {
    .custom instance void System.Runtime.Versioning.NonVersionableAttribute::.ctor()
      = (01 00 00 00 )
    .maxstack 8

    // [134 56 - 134 61]
    IL_0000: ldarg.0      // o
    IL_0001: ret

  } // end of method Unsafe::As

  .method public hidebysig static !!0/*T*/&
    AsRef<T>(
      void* source
    ) cil managed
  {
    .custom instance void System.Runtime.Versioning.NonVersionableAttribute::.ctor()
      = (01 00 00 00 )
    .maxstack 8

    // [138 58 - 138 76]
    IL_0000: ldarg.0      // source
    IL_0001: ret

  } // end of method Unsafe::AsRef

  .method public hidebysig static !!0/*T*/&
    AsRef<T>(
      !!0/*T*/& source
    ) cil managed
  {
    .custom instance void System.Runtime.Versioning.NonVersionableAttribute::.ctor()
      = (01 00 00 00 )
    .param [1]
      .custom instance void System.Runtime.CompilerServices.IsReadOnlyAttribute::.ctor()
        = (01 00 00 00 )
    .maxstack 8

    // [142 50 - 142 60]
    IL_0000: ldarg.0      // source
    IL_0001: ret

  } // end of method Unsafe::AsRef

  .method public hidebysig static !!1/*TTo*/&
    As<TFrom, TTo>(
      !!0/*TFrom*/& source
    ) cil managed
  {
    .custom instance void System.Runtime.Versioning.NonVersionableAttribute::.ctor()
      = (01 00 00 00 )
    .maxstack 8

    // [146 63 - 146 80]
    IL_0000: ldarg.0      // source
    IL_0001: ret

  } // end of method Unsafe::As

  .method public hidebysig static !!0/*T*/&
    Unbox<valuetype .ctor (class System.ValueType) T>(
      object 'box'
    ) cil managed
  {
    .custom instance void System.Runtime.Versioning.NonVersionableAttribute::.ctor()
      = (01 00 00 00 )
    .maxstack 8

    // [150 66 - 150 74]
    IL_0000: ldarg.0      // 'box'
    IL_0001: unbox        !!0/*T*/
    IL_0006: ret

  } // end of method Unsafe::Unbox

  .method public hidebysig static !!0/*T*/&
    Add<T>(
      !!0/*T*/& source,
      int32 elementOffset
    ) cil managed
  {
    .custom instance void System.Runtime.Versioning.NonVersionableAttribute::.ctor()
      = (01 00 00 00 )
    .maxstack 8

    // [157 7 - 157 79]
    IL_0000: ldarg.0      // source
    IL_0001: ldarg.1      // elementOffset
    IL_0002: sizeof       !!0/*T*/
    IL_0008: conv.i
    IL_0009: mul
    IL_000a: add
    IL_000b: ret

  } // end of method Unsafe::Add

  .method public hidebysig static void*
    Add<T>(
      void* source,
      int32 elementOffset
    ) cil managed
  {
    .custom instance void System.Runtime.Versioning.NonVersionableAttribute::.ctor()
      = (01 00 00 00 )
    .maxstack 8

    // [164 7 - 164 78]
    IL_0000: ldarg.0      // source
    IL_0001: ldarg.1      // elementOffset
    IL_0002: sizeof       !!0/*T*/
    IL_0008: conv.i
    IL_0009: mul
    IL_000a: add
    IL_000b: ret

  } // end of method Unsafe::Add

  .method public hidebysig static !!0/*T*/&
    Add<T>(
      !!0/*T*/& source,
      native int elementOffset
    ) cil managed
  {
    .custom instance void System.Runtime.Versioning.NonVersionableAttribute::.ctor()
      = (01 00 00 00 )
    .maxstack 8

    // [172 7 - 172 58]
    IL_0000: ldarg.0      // source
    IL_0001: ldarg.1      // elementOffset
    IL_0002: sizeof       !!0/*T*/
    IL_0008: mul
    IL_0009: add
    IL_000a: ret

  } // end of method Unsafe::Add

  .method public hidebysig static !!0/*T*/&
    AddByteOffset<T>(
      !!0/*T*/& source,
      native int byteOffset
    ) cil managed
  {
    .custom instance void System.Runtime.Versioning.NonVersionableAttribute::.ctor()
      = (01 00 00 00 )
    .maxstack 8

    // [180 7 - 180 54]
    IL_0000: ldarg.0      // source
    IL_0001: ldarg.1      // byteOffset
    IL_0002: add
    IL_0003: ret

  } // end of method Unsafe::AddByteOffset

  .method public hidebysig static !!0/*T*/&
    Subtract<T>(
      !!0/*T*/& source,
      int32 elementOffset
    ) cil managed
  {
    .custom instance void System.Runtime.Versioning.NonVersionableAttribute::.ctor()
      = (01 00 00 00 )
    .maxstack 8

    // [188 7 - 188 79]
    IL_0000: ldarg.0      // source
    IL_0001: ldarg.1      // elementOffset
    IL_0002: sizeof       !!0/*T*/
    IL_0008: conv.i
    IL_0009: mul
    IL_000a: sub
    IL_000b: ret

  } // end of method Unsafe::Subtract

  .method public hidebysig static void*
    Subtract<T>(
      void* source,
      int32 elementOffset
    ) cil managed
  {
    .custom instance void System.Runtime.Versioning.NonVersionableAttribute::.ctor()
      = (01 00 00 00 )
    .maxstack 8

    // [195 7 - 195 78]
    IL_0000: ldarg.0      // source
    IL_0001: ldarg.1      // elementOffset
    IL_0002: sizeof       !!0/*T*/
    IL_0008: conv.i
    IL_0009: mul
    IL_000a: sub
    IL_000b: ret

  } // end of method Unsafe::Subtract

  .method public hidebysig static !!0/*T*/&
    Subtract<T>(
      !!0/*T*/& source,
      native int elementOffset
    ) cil managed
  {
    .custom instance void System.Runtime.Versioning.NonVersionableAttribute::.ctor()
      = (01 00 00 00 )
    .maxstack 8

    // [202 7 - 202 65]
    IL_0000: ldarg.0      // source
    IL_0001: ldarg.1      // elementOffset
    IL_0002: sizeof       !!0/*T*/
    IL_0008: mul
    IL_0009: sub
    IL_000a: ret

  } // end of method Unsafe::Subtract

  .method public hidebysig static !!0/*T*/&
    SubtractByteOffset<T>(
      !!0/*T*/& source,
      native int byteOffset
    ) cil managed
  {
    .custom instance void System.Runtime.Versioning.NonVersionableAttribute::.ctor()
      = (01 00 00 00 )
    .maxstack 8

    // [210 7 - 210 54]
    IL_0000: ldarg.0      // source
    IL_0001: ldarg.1      // byteOffset
    IL_0002: sub
    IL_0003: ret

  } // end of method Unsafe::SubtractByteOffset

  .method public hidebysig static native int
    ByteOffset<T>(
      !!0/*T*/& origin,
      !!0/*T*/& target
    ) cil managed
  {
    .custom instance void System.Runtime.Versioning.NonVersionableAttribute::.ctor()
      = (01 00 00 00 )
    .maxstack 8

    // [217 7 - 217 56]
    IL_0000: ldarg.1      // target
    IL_0001: ldarg.0      // origin
    IL_0002: sub
    IL_0003: ret

  } // end of method Unsafe::ByteOffset

  .method public hidebysig static bool
    AreSame<T>(
      !!0/*T*/& left,
      !!0/*T*/& right
    ) cil managed
  {
    .custom instance void System.Runtime.Versioning.NonVersionableAttribute::.ctor()
      = (01 00 00 00 )
    .maxstack 8

    // [222 63 - 222 84]
    IL_0000: ldarg.0      // left
    IL_0001: ldarg.1      // right
    IL_0002: ceq
    IL_0004: ret

  } // end of method Unsafe::AreSame

  .method public hidebysig static bool
    IsAddressGreaterThan<T>(
      !!0/*T*/& left,
      !!0/*T*/& right
    ) cil managed
  {
    .custom instance void System.Runtime.Versioning.NonVersionableAttribute::.ctor()
      = (01 00 00 00 )
    .maxstack 8

    // [226 76 - 226 96]
    IL_0000: ldarg.0      // left
    IL_0001: ldarg.1      // right
    IL_0002: cgt.un
    IL_0004: ret

  } // end of method Unsafe::IsAddressGreaterThan

  .method public hidebysig static bool
    IsAddressLessThan<T>(
      !!0/*T*/& left,
      !!0/*T*/& right
    ) cil managed
  {
    .custom instance void System.Runtime.Versioning.NonVersionableAttribute::.ctor()
      = (01 00 00 00 )
    .maxstack 8

    // [230 73 - 230 93]
    IL_0000: ldarg.0      // left
    IL_0001: ldarg.1      // right
    IL_0002: clt.un
    IL_0004: ret

  } // end of method Unsafe::IsAddressLessThan
} // end of class System.Runtime.CompilerServices.Unsafe
