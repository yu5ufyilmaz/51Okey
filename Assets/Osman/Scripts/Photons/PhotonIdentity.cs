public static class PhotonIdentity
{
    // Photon kimliği Firebase uid'i değilse (ilk açılışta giriş bağlanmadan önce olur) yeniden bağlanmak gerekir.
    public static bool NeedsReconnect(string photonUserId, string authUserId) =>
        authUserId != null && photonUserId != authUserId;
}
