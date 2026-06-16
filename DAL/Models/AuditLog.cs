using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace DAL.Models
{
    

    [Table("audit_logs")]
    public class AuditLog
    {
        [Key]
        [Column("audit_log_id")]
        public Guid AuditLogId { get; set; }

        [Column("user_id")]
        public Guid? UserId { get; set; }

        [Column("action")]
        public string Action { get; set; } = null!;

        [Column("table_name")]
        public string TableName { get; set; } = null!;

        [Column("record_id")]
        public string? RecordId { get; set; }

        [Column("old_values")]
        public string? OldValues { get; set; }

        [Column("new_values")]
        public string? NewValues { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}
