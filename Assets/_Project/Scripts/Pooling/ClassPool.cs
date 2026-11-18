using Zenject;

// Pool dành cho class C# thuần; không tạo GameObject và không phụ thuộc vòng đời MonoBehaviour.
// Bên sử dụng phải dọn trạng thái của object trước khi gọi Despawn.
public sealed class ClassPool<T> : MemoryPool<T> where T : class { }
