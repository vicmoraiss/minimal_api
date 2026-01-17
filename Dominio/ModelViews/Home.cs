namespace MinimalApi.Dominio.ModelViews;

public struct Home
{
    public string Mensagem { get => "API Minimal funcionando!"; }  
    public string Doc { get => "/swagger"; }
}