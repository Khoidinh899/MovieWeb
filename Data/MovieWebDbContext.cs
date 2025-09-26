using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using MovieWeb.Models.Entities;

namespace MovieWeb.Data;

public partial class MovieWebDbContext : DbContext
{
    public MovieWebDbContext()
    {
    }

    public MovieWebDbContext(DbContextOptions<MovieWebDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Actor> Actors { get; set; }

    public virtual DbSet<AdminLog> AdminLogs { get; set; }

    public virtual DbSet<Category> Categories { get; set; }

    public virtual DbSet<Comment> Comments { get; set; }

    public virtual DbSet<Country> Countries { get; set; }

    public virtual DbSet<Director> Directors { get; set; }

    public virtual DbSet<Favorite> Favorites { get; set; }

    public virtual DbSet<Movie> Movies { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<Rating> Ratings { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<WatchHistory> WatchHistories { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Actor>(entity =>
        {
            entity.HasKey(e => e.ActorId).HasName("PK__Actors__57B3EA4B1F26927F");

            entity.HasIndex(e => e.Slug, "UQ__Actors__BC7B5FB688EA0EF4").IsUnique();

            entity.Property(e => e.Avatar).HasMaxLength(500);
            entity.Property(e => e.Biography).HasColumnType("ntext");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Slug).HasMaxLength(100);
        });

        modelBuilder.Entity<AdminLog>(entity =>
        {
            entity.HasKey(e => e.LogId).HasName("PK__AdminLog__5E5486482B3552F5");

            entity.Property(e => e.Action).HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.IpAddress).HasMaxLength(50);
            entity.Property(e => e.NewValue).HasColumnType("ntext");
            entity.Property(e => e.OldValue).HasColumnType("ntext");
            entity.Property(e => e.TableName).HasMaxLength(50);
            entity.Property(e => e.UserAgent).HasMaxLength(500);

            entity.HasOne(d => d.Admin).WithMany(p => p.AdminLogs)
                .HasForeignKey(d => d.AdminId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__AdminLogs__Admin__17036CC0");
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.CategoryId).HasName("PK__Categori__19093A0BE8B0B1CA");

            entity.HasIndex(e => e.Slug, "UQ__Categori__BC7B5FB63CACE86B").IsUnique();

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Slug).HasMaxLength(100);
        });

        modelBuilder.Entity<Comment>(entity =>
        {
            entity.HasKey(e => e.CommentId).HasName("PK__Comments__C3B4DFCADA06B684");

            entity.HasIndex(e => e.MovieId, "IX_Comments_MovieId");

            entity.Property(e => e.Content).HasColumnType("ntext");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Movie).WithMany(p => p.Comments)
                .HasForeignKey(d => d.MovieId)
                .HasConstraintName("FK__Comments__MovieI__02FC7413");

            entity.HasOne(d => d.ParentComment).WithMany(p => p.InverseParentComment)
                .HasForeignKey(d => d.ParentCommentId)
                .HasConstraintName("FK__Comments__Parent__03F0984C");

            entity.HasOne(d => d.User).WithMany(p => p.Comments)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK__Comments__UserId__02084FDA");
        });

        modelBuilder.Entity<Country>(entity =>
        {
            entity.HasKey(e => e.CountryId).HasName("PK__Countrie__10D1609F99BB0BFD");

            entity.HasIndex(e => e.Slug, "UQ__Countrie__BC7B5FB6D85F4721").IsUnique();

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Slug).HasMaxLength(100);
        });

        modelBuilder.Entity<Director>(entity =>
        {
            entity.HasKey(e => e.DirectorId).HasName("PK__Director__26C69E467ECC4538");

            entity.HasIndex(e => e.Slug, "UQ__Director__BC7B5FB6E39C98C0").IsUnique();

            entity.Property(e => e.Avatar).HasMaxLength(500);
            entity.Property(e => e.Biography).HasColumnType("ntext");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Slug).HasMaxLength(100);
        });

        modelBuilder.Entity<Favorite>(entity =>
        {
            entity.HasKey(e => e.FavoriteId).HasName("PK__Favorite__CE74FAD59B321046");

            entity.HasIndex(e => e.UserId, "IX_Favorites_UserId");

            entity.HasIndex(e => new { e.UserId, e.MovieId }, "UQ__Favorite__A335E50C0B1A2B81").IsUnique();

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Movie).WithMany(p => p.Favorites)
                .HasForeignKey(d => d.MovieId)
                .HasConstraintName("FK__Favorites__Movie__74AE54BC");

            entity.HasOne(d => d.User).WithMany(p => p.Favorites)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK__Favorites__UserI__73BA3083");
        });

        modelBuilder.Entity<Movie>(entity =>
        {
            entity.HasKey(e => e.MovieId).HasName("PK__Movies__4BD2941AB82DFE56");

            entity.HasIndex(e => e.Rating, "IX_Movies_Rating").IsDescending();

            entity.HasIndex(e => e.Slug, "IX_Movies_Slug");

            entity.HasIndex(e => e.Type, "IX_Movies_Type");

            entity.HasIndex(e => e.ViewCount, "IX_Movies_ViewCount").IsDescending();

            entity.HasIndex(e => e.Year, "IX_Movies_Year");

            entity.HasIndex(e => e.Slug, "UQ__Movies__BC7B5FB6B116A7B6").IsUnique();

            entity.Property(e => e.ApiId).HasMaxLength(50);
            entity.Property(e => e.Content).HasColumnType("ntext");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.EpisodeCurrent).HasMaxLength(50);
            entity.Property(e => e.EpisodeTotal).HasMaxLength(50);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsCopyright).HasDefaultValue(false);
            entity.Property(e => e.IsRecommended).HasDefaultValue(false);
            entity.Property(e => e.Language).HasMaxLength(50);
            entity.Property(e => e.Name).HasMaxLength(255);
            entity.Property(e => e.OriginalName).HasMaxLength(255);
            entity.Property(e => e.PosterUrl).HasMaxLength(500);
            entity.Property(e => e.Quality).HasMaxLength(50);
            entity.Property(e => e.Rating)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(3, 1)");
            entity.Property(e => e.RatingCount).HasDefaultValue(0);
            entity.Property(e => e.Slug).HasMaxLength(200);
            entity.Property(e => e.Status).HasMaxLength(50);
            entity.Property(e => e.ThumbUrl).HasMaxLength(500);
            entity.Property(e => e.Time).HasMaxLength(50);
            entity.Property(e => e.TrailerUrl).HasMaxLength(500);
            entity.Property(e => e.Type).HasMaxLength(50);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.ViewCount).HasDefaultValue(0);

            entity.HasMany(d => d.Actors).WithMany(p => p.Movies)
                .UsingEntity<Dictionary<string, object>>(
                    "MovieActor",
                    r => r.HasOne<Actor>().WithMany()
                        .HasForeignKey("ActorId")
                        .HasConstraintName("FK__MovieActo__Actor__6B24EA82"),
                    l => l.HasOne<Movie>().WithMany()
                        .HasForeignKey("MovieId")
                        .HasConstraintName("FK__MovieActo__Movie__6A30C649"),
                    j =>
                    {
                        j.HasKey("MovieId", "ActorId").HasName("PK__MovieAct__EEA9AABE4EA61940");
                        j.ToTable("MovieActors");
                    });

            entity.HasMany(d => d.Categories).WithMany(p => p.Movies)
                .UsingEntity<Dictionary<string, object>>(
                    "MovieCategory",
                    r => r.HasOne<Category>().WithMany()
                        .HasForeignKey("CategoryId")
                        .HasConstraintName("FK__MovieCate__Categ__59FA5E80"),
                    l => l.HasOne<Movie>().WithMany()
                        .HasForeignKey("MovieId")
                        .HasConstraintName("FK__MovieCate__Movie__59063A47"),
                    j =>
                    {
                        j.HasKey("MovieId", "CategoryId").HasName("PK__MovieCat__EA4207BAA3EAFAAD");
                        j.ToTable("MovieCategories");
                    });

            entity.HasMany(d => d.Countries).WithMany(p => p.Movies)
                .UsingEntity<Dictionary<string, object>>(
                    "MovieCountry",
                    r => r.HasOne<Country>().WithMany()
                        .HasForeignKey("CountryId")
                        .HasConstraintName("FK__MovieCoun__Count__5DCAEF64"),
                    l => l.HasOne<Movie>().WithMany()
                        .HasForeignKey("MovieId")
                        .HasConstraintName("FK__MovieCoun__Movie__5CD6CB2B"),
                    j =>
                    {
                        j.HasKey("MovieId", "CountryId").HasName("PK__MovieCou__AADF82134A71A76F");
                        j.ToTable("MovieCountries");
                    });

            entity.HasMany(d => d.Directors).WithMany(p => p.Movies)
                .UsingEntity<Dictionary<string, object>>(
                    "MovieDirector",
                    r => r.HasOne<Director>().WithMany()
                        .HasForeignKey("DirectorId")
                        .HasConstraintName("FK__MovieDire__Direc__6EF57B66"),
                    l => l.HasOne<Movie>().WithMany()
                        .HasForeignKey("MovieId")
                        .HasConstraintName("FK__MovieDire__Movie__6E01572D"),
                    j =>
                    {
                        j.HasKey("MovieId", "DirectorId").HasName("PK__MovieDir__39BEFDFE237775CE");
                        j.ToTable("MovieDirectors");
                    });
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.NotificationId).HasName("PK__Notifica__20CF2E1229953B40");

            entity.HasIndex(e => e.UserId, "IX_Notifications_UserId");

            entity.Property(e => e.Content).HasColumnType("ntext");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsRead).HasDefaultValue(false);
            entity.Property(e => e.Title).HasMaxLength(255);
            entity.Property(e => e.Type)
                .HasMaxLength(50)
                .HasDefaultValue("info");
            entity.Property(e => e.Url).HasMaxLength(500);

            entity.HasOne(d => d.User).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK__Notificat__UserI__1332DBDC");
        });

        modelBuilder.Entity<Rating>(entity =>
        {
            entity.HasKey(e => e.RatingId).HasName("PK__Ratings__FCCDF87CCCFCF95A");

            entity.HasIndex(e => new { e.UserId, e.MovieId }, "UQ__Ratings__A335E50C987AA62A").IsUnique();

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Rating1).HasColumnName("Rating");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Movie).WithMany(p => p.Ratings)
                .HasForeignKey(d => d.MovieId)
                .HasConstraintName("FK__Ratings__MovieId__7C4F7684");

            entity.HasOne(d => d.User).WithMany(p => p.Ratings)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK__Ratings__UserId__7B5B524B");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.RoleId).HasName("PK__Roles__8AFACE1A931EB7E5");

            entity.HasIndex(e => e.RoleName, "UQ__Roles__8A2B6160155A11B3").IsUnique();

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Description).HasMaxLength(255);
            entity.Property(e => e.RoleName).HasMaxLength(50);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__Users__1788CC4CC2DA8064");

            entity.HasIndex(e => e.Email, "IX_Users_Email");

            entity.HasIndex(e => e.Username, "IX_Users_Username");

            entity.HasIndex(e => e.Username, "UQ__Users__536C85E4B56F990C").IsUnique();

            entity.HasIndex(e => e.Email, "UQ__Users__A9D105345763F5F3").IsUnique();

            entity.Property(e => e.Avatar).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.EmailConfirmToken).HasMaxLength(255);
            entity.Property(e => e.FirstName).HasMaxLength(50);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsEmailConfirmed).HasDefaultValue(false);
            entity.Property(e => e.LastName).HasMaxLength(50);
            entity.Property(e => e.PasswordHash).HasMaxLength(255);
            entity.Property(e => e.PasswordResetToken).HasMaxLength(255);
            entity.Property(e => e.RoleId).HasDefaultValue(2);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Username).HasMaxLength(50);

            entity.HasOne(d => d.Role).WithMany(p => p.Users)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Users__RoleId__4222D4EF");
        });

        modelBuilder.Entity<WatchHistory>(entity =>
        {
            entity.HasKey(e => e.HistoryId).HasName("PK__WatchHis__4D7B4ABD26257E5D");

            entity.ToTable("WatchHistory");

            entity.HasIndex(e => e.UserId, "IX_WatchHistory_UserId");

            entity.HasIndex(e => new { e.UserId, e.MovieId, e.EpisodeNumber }, "UQ__WatchHis__8B23F7CD32F4524D").IsUnique();

            entity.Property(e => e.EpisodeNumber).HasDefaultValue(1);
            entity.Property(e => e.IsCompleted).HasDefaultValue(false);
            entity.Property(e => e.LastWatchedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.TotalDuration).HasDefaultValue(0);
            entity.Property(e => e.WatchedDuration).HasDefaultValue(0);

            entity.HasOne(d => d.Movie).WithMany(p => p.WatchHistories)
                .HasForeignKey(d => d.MovieId)
                .HasConstraintName("FK__WatchHist__Movie__0D7A0286");

            entity.HasOne(d => d.User).WithMany(p => p.WatchHistories)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK__WatchHist__UserI__0C85DE4D");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
