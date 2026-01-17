using System.Data.Common;
using MinimalApi.Dominio.Interfaces;
using MinimalApi.DTOs;
using MinimalApi.Dominio.Servicos;
using Microsoft.VisualBasic;
using MinimalApi.Infraestrutura.Db;
using MinimalApi.Dominio.Entidades;

namespace MinimalApi.Dominio.Servicos;

public class AdministradorServico : IAdministradorServico
{
    private readonly DbContexto _contexto;
    public AdministradorServico(DbContexto contexto)
    {
        _contexto = contexto;
    }

    public Administrador? Login(LoginDTO loginDTO)
    {
        var adm = _contexto.Administradores.Where(a => a.Email == loginDTO.Email && a.Senha == loginDTO.Senha).FirstOrDefault();
        return adm;
    }

    public List<Administrador> Todos(int? pagina)
    {
        int tamanhoPagina = 10;
        int numeroPagina = pagina ?? 1;

        return _contexto.Administradores
            .Skip((numeroPagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToList();
   }

    public Administrador Incluir(Administrador administrador)
    {
        _ = _contexto.Administradores.Add(administrador);
        _contexto.SaveChanges();
        return _contexto.Administradores.Last();
    }

    public Administrador? ObterPorId(int id)
    {
        return _contexto.Administradores.Where(a => a.Id == id).FirstOrDefault();
    }

    public Administrador BuscarPorId(int id)
    {
        throw new NotImplementedException();
    }

    internal Administrador Login()
    {
        throw new NotImplementedException();
    }

    Administrador IAdministradorServico.Login()
    {
        return Login();
    }
}