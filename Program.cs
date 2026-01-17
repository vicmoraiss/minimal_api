#region Using
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MinimalApi.Dominio.Interfaces;
using MinimalApi.DTOs;
using MinimalApi.Infraestrutura.Db;
using MinimalApi.Dominio.Servicos;
using MinimalApi.Dominio.ModelViews;
using MinimalApi.Dominio.Entidades;
using MinimalApi.Dominio.Enuns;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Models;

#endregion

#region Builder
var builder = WebApplication.CreateBuilder(args);

var key = builder.Configuration.GetSection("Jwt").ToString();
if (string.IsNullOrEmpty(key))
    key = "123456";

builder.Services.AddAuthentication(option =>
{
    option.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    option.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(option =>
    {
        option.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateLifetime = true,
            IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(key)),
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddScoped<IAdministradorServico, AdministradorServico>();
builder.Services.AddScoped<IVeiculoServico, VeiculoServico>();
builder.Services.AddDbContext<DbContexto>
(options =>
    {
        options.UseMySql(
            builder.Configuration.GetConnectionString("mysql"),
            ServerVersion.AutoDetect(builder.Configuration.GetConnectionString("mysql")));
    }
);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Insira o token JWT desta maneira: Bearer {seu token}",
    });
    
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new List<string>()
        }
    });
});

var app = builder.Build();

#endregion

#region  Home
app.MapGet("/", () => Results.Json(new Home())).WithTags("Home");
#endregion

#region Administradores

string GerarTokenJwt(Administrador administrador)
{
    if (!string.IsNullOrEmpty(key)) return string.Empty;
    else
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>()
        {
            new("Email", administrador.Email),
            new("Perfil", administrador.Perfil)
        };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.Now.AddDays(1),
            signingCredentials: credentials
        );
    
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

app.MapPost("/administradores/login", ([FromBody] LoginDTO loginDTO, IAdministradorServico administradorServico) =>
    {
        var admin = administradorServico.Login(loginDTO);
        if (admin != null)
        {
            string token = GerarTokenJwt(admin);
            return Results.Ok(new AdministradorLogado
            {
                Email = admin.Email,
                Perfil = admin.Perfil,
                Token = token
            });
        }
        else
            return Results.Unauthorized();

    }
).WithTags("Administradores");

app.MapGet("/administradores/lista", ([FromQuery] int? pagina, IAdministradorServico administradorServico) =>
    {
        var adms = new List<AdministradorModelView>();
        var administradores = administradorServico.Todos(pagina);
        foreach (var adm in administradores)
        {
            adms.Add(new AdministradorModelView
            {
                ID = adm.Id,
                Email = adm.Email,
                Perfil = adm.Perfil
            });
        }
        return Results.Ok(adms);
    }
).RequireAuthorization().WithTags("Administradores");

app.MapGet("/administradores/{id}", (int id, IAdministradorServico administradorServico) =>
    {
        var administrador = administradorServico.BuscarPorId(id);
        if (administrador == null)
            return Results.NotFound();

        return Results.Ok(new AdministradorModelView
        {
            ID = administrador.Id,
            Email = administrador.Email,
            Perfil = administrador.Perfil
        });
    }
).RequireAuthorization().WithTags("Administradores");

app.MapPost("/administradores", ([FromBody] AdministradorDTO administradorDTO, IAdministradorServico administradorServico) =>
    {
        var validacao = new ErrosDeValidacao
        {
            Mensagens = new List<string>()
        };

        if (string.IsNullOrEmpty(administradorDTO.Email))
            validacao.Mensagens.Add("O email do administrador é obrigatório.");

        if (string.IsNullOrEmpty(administradorDTO.Senha))
            validacao.Mensagens.Add("A senha do administrador é obrigatória.");

        if (administradorDTO.Perfil == null)
            validacao.Mensagens.Add("O perfil do administrador é obrigatório.");

        if (validacao.Mensagens.Count > 0)
            return Results.BadRequest(validacao);

        var administrador = new Administrador
        {
            Email = administradorDTO.Email,
            Senha = administradorDTO.Senha,
            Perfil = administradorDTO.Perfil?.ToString() ?? Perfil.Editor.ToString()
        };

        administradorServico.Incluir(administrador);

        return Results.Created($"/administrador/{administrador.Id}", new AdministradorModelView
        {
            ID = administrador.Id,
            Email = administrador.Email,
            Perfil = administrador.Perfil
        });
    }
).RequireAuthorization().WithTags("Administradores");
#endregion

#region Veiculos

ErrosDeValidacao validaDTO(VeiculoDTO veiculoDTO)
{
    var validacao = new ErrosDeValidacao
    {
        Mensagens = new List<string>()
    };

    if (string.IsNullOrEmpty(veiculoDTO.Nome))
        validacao.Mensagens.Add("O nome do veículo é obrigatório.");
    if (string.IsNullOrEmpty(veiculoDTO.Marca))
        validacao.Mensagens.Add("A marca do veículo é obrigatória.");
    if (veiculoDTO.Ano <= 0)
        validacao.Mensagens.Add("O ano do veículo deve ser maior que zero.");

    return validacao;
}
app.MapPost("/veiculos", ([FromBody] VeiculoDTO veiculoDTO, IVeiculoServico veiculoServico) =>
    {
        var validacao = validaDTO(veiculoDTO);
        if (validacao.Mensagens.Count > 0)
            return Results.BadRequest(validacao);

        var veiculo = new Veiculo
        {
            Nome = veiculoDTO.Nome,
            Marca = veiculoDTO.Marca,
            Ano = veiculoDTO.Ano
        };

        veiculoServico.Incluir(veiculo);

        return Results.Created($"/veiculos/{veiculo.Id}", veiculo);
    }
    ).RequireAuthorization().WithTags("Veiculos");

app.MapGet("/veiculos", ([FromQuery] int? pagina, IVeiculoServico veiculoServico) =>
{
    var veiculos = veiculoServico.Todos(pagina);
    return Results.Ok(veiculos);
}
    ).RequireAuthorization().WithTags("Veiculos");

app.MapGet("/veiculos/{id}", (int id, IVeiculoServico veiculoServico) =>
{
    var veiculo = veiculoServico.ObterPorId(id);
    if (veiculo != null)
        return Results.Ok(veiculo);
    else
        return Results.NotFound();
}
).RequireAuthorization().WithTags("Veiculos");

app.MapPut("/veiculos/{id}", ([FromRoute] int id, VeiculoDTO veiculoDTO, IVeiculoServico veiculoServico) =>
{
    var validacao = validaDTO(veiculoDTO);
    if (validacao.Mensagens.Count > 0)
        return Results.BadRequest(validacao);

    var veiculo = veiculoServico.ObterPorId(id);
    if (veiculo == null)
        return Results.NotFound();
    else
    {
        veiculo.Nome = veiculoDTO.Nome;
        veiculo.Marca = veiculoDTO.Marca;
        veiculo.Ano = veiculoDTO.Ano;

        veiculoServico.Atualizar(veiculo);
        return Results.Ok(veiculo);
    }
}
).RequireAuthorization().WithTags("Veiculos");

app.MapDelete("/veiculos/{id}", ([FromRoute] int id, IVeiculoServico veiculoServico) =>
    {
        var veiculo = veiculoServico.ObterPorId(id);
        if (veiculo == null)
            return Results.NotFound();
        else
            veiculoServico.Apagar(veiculo);
        return Results.NoContent();
    }
).RequireAuthorization().WithTags("Veiculos");
#endregion

#region App
app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

app.Run();

#endregion