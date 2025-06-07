using System;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Windows.Forms;
using System.Globalization; // Diperlukan untuk TryParseExact jika tanggal adalah string

namespace UCP1
{
    public partial class FormTambahTransaksi : Form
    {
        private int idEdit = -1; // Untuk id_transaksi yang akan diedit
        private int idKategoriEdit = -1; // Untuk id_kategori yang terkait dengan transaksi yang diedit
        private string connectionString = "Data Source=OCTAVIANIPTR\\OTHALLIA;Initial Catalog=NYOBA;Integrated Security=True";

        public FormTambahTransaksi()
        {
            InitializeComponent();
            dataGridView1.CellClick += dataGridView1_CellClick;

            // Setup Tombol Lihat Laporan (button1)
            button1.Text = "Lihat Laporan";
            button1.BackColor = Color.LightGreen;
            button1.Font = new Font("Arial", 9, FontStyle.Bold);
            button1.Click += BtnLihatLaporan_Click;
        }

        private void BtnLihatLaporan_Click(object sender, EventArgs e)
        {
            try
            {
                button1.BackColor = Color.Lime;
                button1.Refresh(); // Refresh button untuk update warna

                TampilkanLaporan();

                // Timer untuk mengembalikan warna button setelah beberapa saat
                Timer timer = new Timer { Interval = 300 };
                timer.Tick += (s, args) =>
                {
                    button1.BackColor = Color.LightGreen;
                    timer.Stop();
                    timer.Dispose();
                };
                timer.Start();

                // Auto-scroll ke baris terakhir jika ada data
                if (dataGridView1.Rows.Count > 0 && dataGridView1.Rows.Count > dataGridView1.DisplayedRowCount(false))
                {
                    // Pastikan baris TOTAL tidak menyebabkan error jika itu satu-satunya baris setelah filter misalnya
                    int lastDataRowIndex = dataGridView1.Rows.Count - 1;
                    if (dataGridView1.Rows[lastDataRowIndex].Cells["nama_kategori"].Value.ToString() == "TOTAL" && dataGridView1.Rows.Count > 1)
                    {
                        lastDataRowIndex--; // Scroll ke baris data sebelum TOTAL jika ada
                    }
                     if (lastDataRowIndex >=0) dataGridView1.FirstDisplayedScrollingRowIndex = lastDataRowIndex;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gagal memuat laporan: {ex.Message}", "Error",
                                 MessageBoxButtons.OK, MessageBoxIcon.Error);
                button1.BackColor = Color.Salmon; // Warna error
            }
        }

        private void FormTambahTransaksi_Load(object sender, EventArgs e)
        {
            comboBox1.Items.Add("pemasukan");
            comboBox1.Items.Add("pengeluaran");
            comboBox1.SelectedIndex = 0; // Default ke pemasukan
            dateTimePicker1.MinDate = new DateTime(2020, 1, 1); // Batas minimal tanggal
            dateTimePicker1.Value = DateTime.Now; // Default ke tanggal hari ini

            TampilkanLaporan();
        }

        private void btnSimpan_Click(object sender, EventArgs e)
        {
            string namaKategori = textBox1.Text.Trim();
            string jumlahText = textBox2.Text.Trim();
            string keterangan = textBox3.Text.Trim();
            DateTime tanggal = dateTimePicker1.Value;
            string tipeKategori = comboBox1.SelectedItem.ToString();

            // Validasi Nama Kategori
            if (string.IsNullOrWhiteSpace(namaKategori) || !System.Text.RegularExpressions.Regex.IsMatch(namaKategori, @"^[a-zA-Z\s]+$"))
            {
                MessageBox.Show("Nama kategori harus diisi dan hanya terdiri dari huruf serta spasi.", "Validasi Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                textBox1.Focus();
                return;
            }

            // Validasi Jumlah
            if (!decimal.TryParse(jumlahText, out decimal jumlah) || jumlah <= 0)
            {
                MessageBox.Show("Jumlah harus berupa angka valid yang lebih dari 0.", "Validasi Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                textBox2.Focus();
                return;
            }

            // Validasi Tahun Transaksi
            if (tanggal.Year < 2020)
            {
                MessageBox.Show("Data transaksi hanya berlaku untuk tahun 2020 ke atas.", "Validasi Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                dateTimePicker1.Focus();
                return;
            }

            var confirmSave = MessageBox.Show("Apakah Anda yakin ingin menyimpan data ini?",
                                             "Konfirmasi Simpan", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirmSave != DialogResult.Yes)
                return;

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                SqlTransaction sqlTransaction = conn.BeginTransaction(); // Mulai transaksi database

                try
                {
                    if (idEdit == -1) // Mode INSERT (Tambah Data Baru)
                    {
                        // 1. Insert ke tabel kategori
                        string insertKategoriQuery = "INSERT INTO kategori (nama_kategori, tipe) VALUES (@nama_kategori, @tipe); SELECT SCOPE_IDENTITY();";
                        SqlCommand cmdKategori = new SqlCommand(insertKategoriQuery, conn, sqlTransaction);
                        cmdKategori.Parameters.AddWithValue("@nama_kategori", namaKategori);
                        cmdKategori.Parameters.AddWithValue("@tipe", tipeKategori);
                        int newKategoriId = Convert.ToInt32(cmdKategori.ExecuteScalar());

                        // 2. Insert ke tabel transaksi
                        string insertTransaksiQuery = "INSERT INTO transaksi (id_kategori, jumlah, tanggal, keterangan) VALUES (@id_kategori, @jumlah, @tanggal, @keterangan)";
                        SqlCommand cmdTransaksi = new SqlCommand(insertTransaksiQuery, conn, sqlTransaction);
                        cmdTransaksi.Parameters.AddWithValue("@id_kategori", newKategoriId);
                        cmdTransaksi.Parameters.AddWithValue("@jumlah", jumlah);
                        cmdTransaksi.Parameters.AddWithValue("@tanggal", tanggal);
                        cmdTransaksi.Parameters.AddWithValue("@keterangan", keterangan);
                        cmdTransaksi.ExecuteNonQuery();

                        MessageBox.Show("Transaksi berhasil ditambahkan.", "Sukses", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else // Mode UPDATE (Edit Data yang Ada)
                    {
                        if (idKategoriEdit == -1) // Pemeriksaan keamanan tambahan
                        {
                            MessageBox.Show("Error: ID Kategori untuk update tidak valid. Coba pilih ulang data.", "Error Update", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            sqlTransaction.Rollback();
                            return;
                        }

                        // 1. Update tabel kategori
                        string updateKategoriQuery = "UPDATE kategori SET nama_kategori = @nama_kategori, tipe = @tipe WHERE id_kategori = @id_kategori_edit";
                        SqlCommand cmdUpdateKategori = new SqlCommand(updateKategoriQuery, conn, sqlTransaction);
                        cmdUpdateKategori.Parameters.AddWithValue("@nama_kategori", namaKategori);
                        cmdUpdateKategori.Parameters.AddWithValue("@tipe", tipeKategori);
                        cmdUpdateKategori.Parameters.AddWithValue("@id_kategori_edit", idKategoriEdit);
                        cmdUpdateKategori.ExecuteNonQuery();

                        // 2. Update tabel transaksi
                        string updateTransaksiQuery = "UPDATE transaksi SET jumlah=@jumlah, tanggal=@tanggal, keterangan=@keterangan, id_kategori=@id_kategori_update WHERE id_transaksi=@id_transaksi_edit";
                        SqlCommand cmdUpdateTransaksi = new SqlCommand(updateTransaksiQuery, conn, sqlTransaction);
                        cmdUpdateTransaksi.Parameters.AddWithValue("@jumlah", jumlah);
                        cmdUpdateTransaksi.Parameters.AddWithValue("@tanggal", tanggal);
                        cmdUpdateTransaksi.Parameters.AddWithValue("@keterangan", keterangan);
                        cmdUpdateTransaksi.Parameters.AddWithValue("@id_kategori_update", idKategoriEdit); // Tetap menggunakan idKategoriEdit karena kategori yang sama diupdate
                        cmdUpdateTransaksi.Parameters.AddWithValue("@id_transaksi_edit", idEdit);
                        cmdUpdateTransaksi.ExecuteNonQuery();

                        MessageBox.Show("Transaksi berhasil diupdate.", "Sukses", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }

                    sqlTransaction.Commit(); // Jika semua perintah SQL berhasil, simpan perubahan
                }
                catch (SqlException ex)
                {
                    sqlTransaction.Rollback(); // Jika ada error SQL, batalkan semua perubahan
                    MessageBox.Show($"Database error: {ex.Message}\nSQL Error Code: {ex.Number}", "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                catch (Exception ex)
                {
                    sqlTransaction.Rollback(); // Jika ada error lain, batalkan semua perubahan
                    MessageBox.Show($"An error occurred: {ex.Message}", "General Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally // Reset ID setelah operasi selesai, baik sukses maupun gagal
                {
                    idEdit = -1;
                    idKategoriEdit = -1;
                }
            }

            ClearForm();
            TampilkanLaporan();
        }

        private void dataGridView1_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return; // Klik di header atau di luar batas

                DataGridViewRow row = dataGridView1.Rows[e.RowIndex];

                // Abaikan jika yang diklik adalah baris TOTAL
                if (row.Cells["nama_kategori"].Value != null && row.Cells["nama_kategori"].Value.ToString() == "TOTAL")
                {
                    return;
                }


                string clickedColumnName = dataGridView1.Columns[e.ColumnIndex].Name;

                // Pastikan id_transaksi dan id_kategori ada dan valid sebelum melanjutkan
                if (row.Cells["id_transaksi"].Value == DBNull.Value || row.Cells["id_transaksi"].Value == null) return;
                int currentIdTransaksi = Convert.ToInt32(row.Cells["id_transaksi"].Value);


                if (clickedColumnName == "Update")
                {
                     if (row.Cells["id_kategori"].Value == DBNull.Value || row.Cells["id_kategori"].Value == null)
                    {
                        MessageBox.Show("ID Kategori tidak valid untuk baris ini.", "Error Data", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    int currentIdKategori = Convert.ToInt32(row.Cells["id_kategori"].Value);

                    var confirmUpdate = MessageBox.Show("Apakah Anda yakin ingin mengisi form dengan data ini untuk diupdate?",
                                                       "Konfirmasi Update", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (confirmUpdate != DialogResult.Yes) return;

                    textBox1.Text = row.Cells["nama_kategori"].Value.ToString();
                    comboBox1.SelectedItem = row.Cells["tipe"].Value.ToString();
                    textBox2.Text = row.Cells["jumlah"].Value.ToString();
                    textBox3.Text = row.Cells["keterangan"].Value.ToString();

                    // Penanganan Tanggal:
                    // Jika kolom 'tanggal' di DataGridView adalah string (misal dari CONVERT 103 di SQL view),
                    // Anda perlu parsing seperti ini:
                    // if (DateTime.TryParseExact(row.Cells["tanggal"].Value.ToString(), "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedDate))
                    // {
                    //     dateTimePicker1.Value = parsedDate;
                    // }
                    // Jika kolom 'tanggal' sudah DateTime (lebih baik), cukup:
                    if (row.Cells["tanggal"].Value != DBNull.Value)
                    {
                        dateTimePicker1.Value = Convert.ToDateTime(row.Cells["tanggal"].Value);
                    }


                    idEdit = currentIdTransaksi;         // Simpan id_transaksi untuk proses update
                    idKategoriEdit = currentIdKategori; // Simpan id_kategori untuk proses update
                }
                else if (clickedColumnName == "Delete")
                {
                    var confirmDelete = MessageBox.Show("Yakin ingin menghapus transaksi ini?", "Konfirmasi Hapus", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (confirmDelete == DialogResult.Yes)
                    {
                        using (SqlConnection conn = new SqlConnection(connectionString))
                        {
                            conn.Open();
                            // Pertimbangkan menggunakan SqlTransaction jika proses delete melibatkan banyak tabel atau ada trigger kompleks
                            SqlCommand cmd = new SqlCommand("DELETE FROM transaksi WHERE id_transaksi = @id", conn);
                            cmd.Parameters.AddWithValue("@id", currentIdTransaksi);
                            cmd.ExecuteNonQuery();
                            MessageBox.Show("Transaksi berhasil dihapus.", "Sukses", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            TampilkanLaporan(); // Refresh data di grid
                            ClearForm();      // Bersihkan form jika data yang sama sedang diedit
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error pada operasi DataGridView: {ex.Message}\n\nStackTrace: {ex.StackTrace}", "Error Grid", MessageBoxButtons.OK, MessageBoxIcon.Error);
                 // Saat error, pastikan ID reset agar tidak terjadi update/insert yang salah
                idEdit = -1;
                idKategoriEdit = -1;
            }
        }

        private void TampilkanLaporan()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    // Pastikan view_transaksi_lengkap menyertakan id_kategori
                    string query = @"SELECT id_transaksi, id_kategori, nama_kategori, tipe, jumlah, tanggal, keterangan
                                     FROM view_transaksi_lengkap
                                     ORDER BY tanggal DESC, id_transaksi DESC"; // Urutkan berdasarkan tanggal terbaru

                    SqlDataAdapter adapter = new SqlDataAdapter(query, conn);
                    DataTable dt = new DataTable();
                    adapter.Fill(dt);

                    // Hitung Total Pemasukan dan Pengeluaran
                    decimal totalPemasukan = 0, totalPengeluaran = 0;
                    foreach (DataRow row in dt.Rows)
                    {
                        if (row["tipe"] != DBNull.Value)
                        {
                            if (row["tipe"].ToString().ToLower() == "pemasukan")
                                totalPemasukan += Convert.ToDecimal(row["jumlah"]);
                            else if (row["tipe"].ToString().ToLower() == "pengeluaran")
                                totalPengeluaran += Convert.ToDecimal(row["jumlah"]);
                        }
                    }

                    // Tambahkan Baris TOTAL di akhir DataTable
                    DataRow totalRow = dt.NewRow();
                    totalRow["id_transaksi"] = DBNull.Value; // Atau nilai sentinel lain jika diperlukan
                    totalRow["id_kategori"] = DBNull.Value;
                    totalRow["nama_kategori"] = "TOTAL";
                    totalRow["tipe"] = DBNull.Value;
                    totalRow["jumlah"] = DBNull.Value; // Jumlah tidak relevan untuk baris total di kolom ini
                    totalRow["tanggal"] = DBNull.Value;
                    totalRow["keterangan"] = $"Pemasukan: Rp{totalPemasukan:N0}  |  Pengeluaran: Rp{totalPengeluaran:N0}  |  Saldo: Rp{(totalPemasukan - totalPengeluaran):N0}";
                    dt.Rows.Add(totalRow);

                    SetupDataGridView(dt);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saat menampilkan laporan: {ex.Message}", "Error Laporan", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SetupDataGridView(DataTable dt)
        {
            dataGridView1.DataSource = null; // Hapus source lama
            dataGridView1.Columns.Clear();   // Hapus kolom lama
            dataGridView1.AutoGenerateColumns = false; // Nonaktifkan pembuatan kolom otomatis

            // Definisikan Kolom Data (sesuaikan DataPropertyName dengan nama kolom di DataTable/View)
            AddColumn("id_transaksi", "ID Transaksi", false); // Kolom ID biasanya disembunyikan
            AddColumn("id_kategori", "ID Kategori", false);  // Kolom ID Kategori juga disembunyikan
            AddColumn("nama_kategori", "Kategori");
            AddColumn("tipe", "Jenis");
            AddColumn("jumlah", "Jumlah", true, "N0", DataGridViewContentAlignment.MiddleRight); // Format angka
            AddColumn("tanggal", "Tanggal", true, "dd/MM/yyyy"); // Format tanggal, pastikan tipe data di DataTable sesuai
            AddColumn("keterangan", "Keterangan");

            // Definisikan Kolom Tombol Aksi
            AddButtonColumn("Update", "Update", Color.LightBlue);
            AddButtonColumn("Delete", "Delete", Color.LightCoral);

            dataGridView1.DataSource = dt; // Set source data baru

            FormatGridAppearance();
        }

        private void AddColumn(string dataPropertyName, string headerText, bool visible = true,
                               string format = null, DataGridViewContentAlignment alignment = DataGridViewContentAlignment.MiddleLeft)
        {
            var col = new DataGridViewTextBoxColumn
            {
                Name = dataPropertyName, // Name bisa sama dengan DataPropertyName agar mudah diakses
                DataPropertyName = dataPropertyName,
                HeaderText = headerText,
                Visible = visible,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells, // Ukuran kolom
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = alignment }
            };

            if (!string.IsNullOrEmpty(format))
            {
                col.DefaultCellStyle.Format = format;
            }
            dataGridView1.Columns.Add(col);
        }

        private void AddButtonColumn(string name, string text, Color backColor)
        {
            var btnCol = new DataGridViewButtonColumn
            {
                Name = name,
                Text = text,
                HeaderText = "Aksi",
                UseColumnTextForButtonValue = true, // Tombol akan menampilkan Text di atas
                FlatStyle = FlatStyle.Flat,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = backColor,
                    ForeColor = Color.Black, // Warna teks tombol
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    Padding = new Padding(2)
                },
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCellsExceptHeader // Ukuran kolom tombol
            };
            dataGridView1.Columns.Add(btnCol);
        }

        private void FormatGridAppearance()
        {
            dataGridView1.ColumnHeadersDefaultCellStyle.Font = new Font("Arial", 9, FontStyle.Bold);
            dataGridView1.EnableHeadersVisualStyles = false; // Memungkinkan kustomisasi header
            dataGridView1.ColumnHeadersDefaultCellStyle.BackColor = Color.LightGray;
            dataGridView1.ColumnHeadersDefaultCellStyle.ForeColor = Color.Black;
            dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill; // Sesuaikan ukuran kolom otomatis
            dataGridView1.RowHeadersVisible = false; // Sembunyikan header baris
            dataGridView1.AllowUserToAddRows = false; // Jangan biarkan user menambah baris langsung di grid
            dataGridView1.AllowUserToDeleteRows = false; // Jangan biarkan user menghapus baris langsung di grid
            dataGridView1.SelectionMode = DataGridViewSelectionMode.FullRowSelect; // Pilih seluruh baris
            dataGridView1.MultiSelect = false; // Hanya satu baris yang bisa dipilih

            // Format baris TOTAL jika ada
            if (dataGridView1.Rows.Count > 0)
            {
                DataGridViewRow lastRow = dataGridView1.Rows[dataGridView1.Rows.Count - 1];
                if (lastRow.Cells["nama_kategori"].Value != null && lastRow.Cells["nama_kategori"].Value.ToString() == "TOTAL")
                {
                    lastRow.DefaultCellStyle.Font = new Font(dataGridView1.Font, FontStyle.Bold);
                    lastRow.DefaultCellStyle.BackColor = Color.LightYellow; // Warna latar beda untuk total
                    // Membuat kolom Update dan Delete tidak bisa diklik/kosong untuk baris TOTAL
                    lastRow.Cells["Update"].Value = string.Empty;
                    lastRow.Cells["Delete"].Value = string.Empty;
                 }
            }
        }

        private void ClearForm()
        {
            textBox1.Clear();
            textBox2.Clear();
            textBox3.Clear();
            comboBox1.SelectedIndex = 0; // Atau -1 jika ingin kosong tanpa pilihan default
            dateTimePicker1.Value = DateTime.Now;
            idEdit = -1;        // Reset ID transaksi yang diedit
            idKategoriEdit = -1; // Reset ID kategori yang diedit
            textBox1.Focus();   // Fokuskan kembali ke input pertama
        }

        private void Import(object sender, EventArgs e)
        {

        }
    }
}