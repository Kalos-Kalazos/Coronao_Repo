// INetworkAuthority.cs
using System;

/// <summary>
/// Interfaz mínima que separa la validación/autoridad del juego.
/// Implementa esto en tu TurnManager/Server/AI para validar jugadas.
/// </summary>
public interface INetworkAuthority
{
    /// <summary>
    /// Valida una petición de jugar: devuelve true si el servidor/la IA la acepta.
    /// Puede ser sincrónico (local single-player/AI) o invocar callbacks asíncronos (RPC) en caso de red.
    /// </summary>
    bool ValidatePlayRequest(string playerId, string cardDataId, int slotIndex, out string rejectReason);

    /// <summary>
    /// Notifica al authority de que la jugada fue finalmente confirmada visualmente/animada
    /// y se solicita la resolución de efectos.
    /// </summary>
    void ConfirmPlay(string playerId, string cardInstanceId, int slotIndex);
}

