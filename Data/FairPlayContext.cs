using FairPlay.Models;
using FairPlay.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace FairPlay.Data;

/// <summary>
/// Contexto de Entity Framework Core para FairPlay (SQLite).
///
/// ENFOQUE "DATABASE FIRST": la base de datos se crea EJECUTANDO EL SCRIPT SQL (fairplay.db),
/// y estas clases solo la MAPEAN. NO uses "dotnet ef migrations": las migraciones no pueden
/// reproducir los triggers, las vistas, los CHECK ni el índice parcial del script, y chocarían
/// con la base ya creada.
///
/// Este archivo configura lo que los atributos de Models/ no pueden expresar:
///   1. Enums guardados como TEXTO (los CHECK ... IN (...) del SQL).
///   2. Formato de las fechas (ISO-8601).
///   3. COLLATE NOCASE (no distinguir mayúsculas/minúsculas).
///   4. Índices únicos (UNIQUE) y el índice parcial uq_jugador_capitan.
///   5. Relaciones con su ON DELETE exacto (CASCADE / RESTRICT / SET NULL / NO ACTION).
///   6. Las dos vistas como entidades de solo lectura.
///
/// Registro en Program.cs (antes de builder.Build()):
///     builder.Services.AddDbContext&lt;FairPlayContext&gt;(opciones =&gt;
///         opciones.UseSqlite(builder.Configuration.GetConnectionString("FairPlay")));
///
/// Y en appsettings.json:
///     "ConnectionStrings": { "FairPlay": "Data Source=fairplay.db" }
/// </summary>
public class FairPlayContext : DbContext
{
    public FairPlayContext(DbContextOptions<FairPlayContext> options)
        : base(options)
    {
    }

    // ----- Tablas -----
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Torneo> Torneos => Set<Torneo>();
    public DbSet<Fase> Fases => Set<Fase>();
    public DbSet<Grupo> Grupos => Set<Grupo>();
    public DbSet<Equipo> Equipos => Set<Equipo>();
    public DbSet<Jugador> Jugadores => Set<Jugador>();
    public DbSet<ParticipanteFase> ParticipantesFase => Set<ParticipanteFase>();
    public DbSet<Ronda> Rondas => Set<Ronda>();
    public DbSet<Cruce> Cruces => Set<Cruce>();
    public DbSet<Partido> Partidos => Set<Partido>();
    public DbSet<CambioEstadoPartido> CambiosEstadoPartido => Set<CambioEstadoPartido>();
    public DbSet<EventoPartido> EventosPartido => Set<EventoPartido>();

    // ----- Vistas (solo lectura) -----
    public DbSet<TablaPosicion> TablaPosiciones => Set<TablaPosicion>();
    public DbSet<Goleador> Goleadores => Set<Goleador>();

    /// <summary>
    /// Convenciones globales: todo enum se guarda como TEXTO con el nombre del miembro
    /// (ej. EstadoPartido.EnVivo -> 'EnVivo'), que es lo que permiten los CHECK del SQL.
    /// </summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<FormatoTorneo>().HaveConversion<string>();
        configurationBuilder.Properties<EstadoTorneo>().HaveConversion<string>();
        configurationBuilder.Properties<ModoInscripcion>().HaveConversion<string>();
        configurationBuilder.Properties<TipoFase>().HaveConversion<string>();
        configurationBuilder.Properties<EstadoFase>().HaveConversion<string>();
        configurationBuilder.Properties<EstadoInscripcion>().HaveConversion<string>();
        configurationBuilder.Properties<EstadoPartido>().HaveConversion<string>();
        configurationBuilder.Properties<TipoEvento>().HaveConversion<string>();

        // Caso especial: 'Medio campo' (con espacio) <-> PosicionJugador.MedioCampo
        configurationBuilder.Properties<PosicionJugador>().HaveConversion<PosicionJugadorConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ---------------------------------------------------------------- usuario
        modelBuilder.Entity<Usuario>(e =>
        {
            // COLLATE NOCASE + UNIQUE: "Juan" y "juan" son el mismo usuario / correo
            e.Property(x => x.NombreUsuario).UseCollation("NOCASE");
            e.Property(x => x.Email).UseCollation("NOCASE");
            e.HasIndex(x => x.NombreUsuario).IsUnique();
            e.HasIndex(x => x.Email).IsUnique();

            // DEFAULT (datetime('now')) -> mismo formato de texto
            e.Property(x => x.FechaRegistro).HasConversion(new FechaHoraSqliteConverter());
        });

        // ---------------------------------------------------------------- torneo
        modelBuilder.Entity<Torneo>(e =>
        {
            e.HasIndex(x => x.Codigo).IsUnique();

            // id_organizador ... ON DELETE RESTRICT (no se borra un usuario que organiza torneos)
            e.HasOne(x => x.Organizador)
                .WithMany(u => u.TorneosOrganizados)
                .HasForeignKey(x => x.IdOrganizador)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---------------------------------------------------------------- fase
        modelBuilder.Entity<Fase>(e =>
        {
            // UNIQUE (id_torneo, orden)
            e.HasIndex(x => new { x.IdTorneo, x.Orden }).IsUnique();

            // id_torneo ... ON DELETE CASCADE
            e.HasOne(x => x.Torneo)
                .WithMany(t => t.Fases)
                .HasForeignKey(x => x.IdTorneo)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---------------------------------------------------------------- grupo
        modelBuilder.Entity<Grupo>(e =>
        {
            // UNIQUE (id_fase, nombre)
            e.HasIndex(x => new { x.IdFase, x.Nombre }).IsUnique();

            // id_fase ... ON DELETE CASCADE
            e.HasOne(x => x.Fase)
                .WithMany(f => f.Grupos)
                .HasForeignKey(x => x.IdFase)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---------------------------------------------------------------- equipo
        modelBuilder.Entity<Equipo>(e =>
        {
            e.Property(x => x.Nombre).UseCollation("NOCASE");

            // UNIQUE (id_torneo, nombre)  (con NOCASE)
            e.HasIndex(x => new { x.IdTorneo, x.Nombre }).IsUnique();

            // id_torneo ... ON DELETE CASCADE
            e.HasOne(x => x.Torneo)
                .WithMany(t => t.Equipos)
                .HasForeignKey(x => x.IdTorneo)
                .OnDelete(DeleteBehavior.Cascade);

            // id_responsable ... ON DELETE RESTRICT
            e.HasOne(x => x.Responsable)
                .WithMany(u => u.EquiposResponsable)
                .HasForeignKey(x => x.IdResponsable)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---------------------------------------------------------------- jugador
        modelBuilder.Entity<Jugador>(e =>
        {
            // UNIQUE (id_equipo, dorsal)
            e.HasIndex(x => new { x.IdEquipo, x.Dorsal }).IsUnique();

            // Índice único PARCIAL: a lo sumo un capitán por equipo
            //   CREATE UNIQUE INDEX uq_jugador_capitan ON jugador (id_equipo) WHERE es_capitan = 1;
            e.HasIndex(x => x.IdEquipo)
                .IsUnique()
                .HasFilter("es_capitan = 1")
                .HasDatabaseName("uq_jugador_capitan");

            // id_equipo ... ON DELETE CASCADE
            e.HasOne(x => x.Equipo)
                .WithMany(q => q.Jugadores)
                .HasForeignKey(x => x.IdEquipo)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---------------------------------------------------------------- participante_fase
        modelBuilder.Entity<ParticipanteFase>(e =>
        {
            // UNIQUE (id_fase, id_equipo)
            e.HasIndex(x => new { x.IdFase, x.IdEquipo }).IsUnique();

            // id_fase ... ON DELETE CASCADE
            e.HasOne(x => x.Fase)
                .WithMany(f => f.Participantes)
                .HasForeignKey(x => x.IdFase)
                .OnDelete(DeleteBehavior.Cascade);

            // id_equipo ... ON DELETE NO ACTION
            e.HasOne(x => x.Equipo)
                .WithMany(q => q.Participaciones)
                .HasForeignKey(x => x.IdEquipo)
                .OnDelete(DeleteBehavior.NoAction);

            // id_grupo ... ON DELETE SET NULL
            e.HasOne(x => x.Grupo)
                .WithMany(g => g.Participantes)
                .HasForeignKey(x => x.IdGrupo)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ---------------------------------------------------------------- ronda
        modelBuilder.Entity<Ronda>(e =>
        {
            // id_fase ... ON DELETE CASCADE
            e.HasOne(x => x.Fase)
                .WithMany(f => f.Rondas)
                .HasForeignKey(x => x.IdFase)
                .OnDelete(DeleteBehavior.Cascade);

            // id_grupo ... ON DELETE CASCADE.
            // En el SQL esta FK es COMPUESTA (id_grupo, id_fase) -> grupo (id_grupo, id_fase) para
            // asegurar que el grupo sea de la MISMA fase. Esa restricción la aplica SQLite; aquí
            // EF solo necesita saber que id_grupo apunta a grupo.
            // (El índice único uq_ronda_numero usa IFNULL(...), una expresión que EF no puede
            //  describir; vive en la BD y no necesita estar en el modelo.)
            e.HasOne(x => x.Grupo)
                .WithMany(g => g.Rondas)
                .HasForeignKey(x => x.IdGrupo)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---------------------------------------------------------------- cruce
        modelBuilder.Entity<Cruce>(e =>
        {
            // UNIQUE (id_ronda, posicion)
            e.HasIndex(x => new { x.IdRonda, x.Posicion }).IsUnique();

            // id_ronda ... ON DELETE CASCADE
            e.HasOne(x => x.Ronda)
                .WithMany(r => r.Cruces)
                .HasForeignKey(x => x.IdRonda)
                .OnDelete(DeleteBehavior.Cascade);

            // Tres FK hacia equipo, todas ON DELETE NO ACTION (sin colección inversa en Equipo)
            e.HasOne(x => x.EquipoA)
                .WithMany()
                .HasForeignKey(x => x.IdEquipoA)
                .OnDelete(DeleteBehavior.NoAction);

            e.HasOne(x => x.EquipoB)
                .WithMany()
                .HasForeignKey(x => x.IdEquipoB)
                .OnDelete(DeleteBehavior.NoAction);

            e.HasOne(x => x.Ganador)
                .WithMany()
                .HasForeignKey(x => x.IdGanador)
                .OnDelete(DeleteBehavior.NoAction);

            // Auto-referencia: id_cruce_siguiente ... ON DELETE SET NULL
            e.HasOne(x => x.CruceSiguiente)
                .WithMany(c => c.CrucesPrevios)
                .HasForeignKey(x => x.IdCruceSiguiente)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ---------------------------------------------------------------- partido
        modelBuilder.Entity<Partido>(e =>
        {
            // fecha_hora en ISO-8601 con 'T'
            e.Property(x => x.FechaHora).HasConversion(new FechaHoraIsoConverter());

            // UNIQUE (id_cruce, numero_partido)
            e.HasIndex(x => new { x.IdCruce, x.NumeroPartido }).IsUnique();

            // id_ronda ... ON DELETE CASCADE
            e.HasOne(x => x.Ronda)
                .WithMany(r => r.Partidos)
                .HasForeignKey(x => x.IdRonda)
                .OnDelete(DeleteBehavior.Cascade);

            // id_cruce ... ON DELETE CASCADE (opcional)
            e.HasOne(x => x.Cruce)
                .WithMany(c => c.Partidos)
                .HasForeignKey(x => x.IdCruce)
                .OnDelete(DeleteBehavior.Cascade);

            // id_arbitro ... ON DELETE SET NULL
            e.HasOne(x => x.Arbitro)
                .WithMany(u => u.PartidosArbitrados)
                .HasForeignKey(x => x.IdArbitro)
                .OnDelete(DeleteBehavior.SetNull);

            // id_local ... ON DELETE NO ACTION
            e.HasOne(x => x.Local)
                .WithMany(q => q.PartidosComoLocal)
                .HasForeignKey(x => x.IdLocal)
                .OnDelete(DeleteBehavior.NoAction);

            // id_visitante ... ON DELETE NO ACTION (NULL = el local descansa)
            e.HasOne(x => x.Visitante)
                .WithMany(q => q.PartidosComoVisitante)
                .HasForeignKey(x => x.IdVisitante)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // ---------------------------------------------------------------- cambio_estado_partido
        modelBuilder.Entity<CambioEstadoPartido>(e =>
        {
            // DEFAULT (datetime('now')) -> mismo formato de texto
            e.Property(x => x.FechaHora).HasConversion(new FechaHoraSqliteConverter());

            // id_partido ... ON DELETE CASCADE
            e.HasOne(x => x.Partido)
                .WithMany(p => p.CambiosEstado)
                .HasForeignKey(x => x.IdPartido)
                .OnDelete(DeleteBehavior.Cascade);

            // id_usuario ... ON DELETE SET NULL
            e.HasOne(x => x.Usuario)
                .WithMany(u => u.CambiosEstado)
                .HasForeignKey(x => x.IdUsuario)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ---------------------------------------------------------------- evento_partido
        modelBuilder.Entity<EventoPartido>(e =>
        {
            // id_partido ... ON DELETE CASCADE
            e.HasOne(x => x.Partido)
                .WithMany(p => p.Eventos)
                .HasForeignKey(x => x.IdPartido)
                .OnDelete(DeleteBehavior.Cascade);

            // id_equipo ... ON DELETE NO ACTION
            e.HasOne(x => x.Equipo)
                .WithMany(q => q.Eventos)
                .HasForeignKey(x => x.IdEquipo)
                .OnDelete(DeleteBehavior.NoAction);

            // id_jugador ... ON DELETE NO ACTION (NULL = gol sin jugador)
            e.HasOne(x => x.Jugador)
                .WithMany(j => j.Eventos)
                .HasForeignKey(x => x.IdJugador)
                .OnDelete(DeleteBehavior.NoAction);

            // id_registrado_por ... ON DELETE SET NULL
            e.HasOne(x => x.RegistradoPor)
                .WithMany(u => u.EventosRegistrados)
                .HasForeignKey(x => x.IdRegistradoPor)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ---------------------------------------------------------------- vistas (solo lectura)
        modelBuilder.Entity<TablaPosicion>(e =>
        {
            e.HasNoKey();
            e.ToView("vw_tabla_posiciones");
        });

        modelBuilder.Entity<Goleador>(e =>
        {
            e.HasNoKey();
            e.ToView("vw_goleadores");
        });
    }
}
