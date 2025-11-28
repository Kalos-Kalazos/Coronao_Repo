// IPoolable.cs
public interface IPoolable
{
    // Llamado por la pool cuando el objeto es activado/obtenido
    void OnPoolSpawn();

    // Llamado por la pool cuando el objeto es devuelto
    void OnPoolDespawn();
}

