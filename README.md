# Road Tools

**Road Tools 0.1.3** adalah plugin Unity Editor untuk membuat jalan berbasis spline, menambahkan pagar dan lampu, serta menyesuaikan jalan dengan terrain.

Edit bentuk jalan langsung di Scene View. Saat live updates aktif, permukaan jalan, collider, props, dan terrain yang terhubung mengikuti perubahan titik jalan.

## Fitur utama

- **Jalan yang mudah diedit** — bentuk jalur dengan Unity Splines dan atur lebar melalui Road Profile.
- **Pilihan permukaan jalan** — gunakan preset markah atau material sendiri, termasuk material berbeda pada bagian tertentu.
- **Pagar dan lampu** — tambahkan preset bawaan atau prefab sendiri, lalu atur sisi, jarak, dan penempatannya.
- **Celah dan jalur pagar terpisah** — buat akses masuk atau belokkan pagar tanpa mengubah bentuk jalan.
- **Penyesuaian terrain** — hubungkan jalan dengan terrain dan atur ketinggian, clearance, serta texture bands.
- **Sambungan jalan** — gabungkan ujung jalan atau hubungkan cabang sambil mempertahankan jalur yang ada.
- **Bake hasil jalan** — hentikan live editing setelah selesai dan lanjutkan kembali dengan Resume Editing.

## Persyaratan

- Unity **6000.3**. Versi target pengembangan: **6000.3.6f1**.
- Unity Splines **2.8.2**.
- Mathematics **1.3.3**.
- Universal RP **17.3.0**.
- Proyek menggunakan **URP** dengan Render Pipeline Asset yang sudah dikonfigurasi.
- Git terpasang untuk instalasi melalui Git URL.

Dependensi paket dideklarasikan di `package.json`. Material bawaan menggunakan shader URP Lit, sehingga proyek perlu memakai URP agar material tampil dengan benar.

## Instalasi

1. Buka **Window > Package Manager** di Unity.
2. Pilih **Add package from Git URL**.
3. Masukkan URL berikut:

```text
https://github.com/denx4x/Road-Tools.git
```

Untuk memakai versi tertentu, tambahkan tag rilis yang sudah tersedia, misalnya `#v0.1.3` setelah tag tersebut dipublikasikan.

Jika Anda menggunakan salinan paket lokal, pilih **Add package from disk**, lalu buka file `package.json` di folder paket.

## Mulai membuat jalan

1. Buka **Tools > Road Tools > Open Window**.
2. Pada tab **Road**, pilih **Road Profile**, lalu tekan **Create Road From Scene View**.
3. Edit titik jalan atau *knot* di Scene View. Gunakan **W** untuk menggeser dan **E** untuk memutar.
4. Pilih **Surface Preset** untuk mengubah tampilan jalan, atau gunakan **Material Override** untuk material sendiri.
5. Buka tab **Props** untuk menambahkan pagar atau lampu.
6. Jika menggunakan terrain, hubungkan melalui tab **Terrain**.
7. Simpan scene untuk menyimpan pekerjaan Anda.

Untuk menggunakan spline yang sudah ada, pilih GameObject spline, lalu tekan **Set Up Road Tools on Selected GameObject**.

## Mengatur tampilan jalan

Pada tab **Road > ROAD MATERIAL**, pilih salah satu preset:

| Preset | Tampilan |
|---|---|
| Profile Default | Menggunakan material dari Road Profile |
| Fully Marked | Markah jalan lengkap |
| Left Edge + Center | Garis tepi kiri dan garis tengah |
| Right Edge + Center | Garis tepi kanan dan garis tengah |
| Edge Lines Only | Garis tepi tanpa garis tengah |
| Unmarked Asphalt | Asphalt tanpa markah |

Sisi kiri dan kanan mengikuti arah spline.

Untuk mengganti material hanya pada bagian tertentu, buka **LOCAL MATERIALS**:

- **+ By Distance**: tentukan batas awal dan akhir dalam meter.
- **+ From Knots**: gunakan knot yang dipilih sebagai batas bagian jalan.
- Aktifkan **Show Section in Scene** untuk melihat area yang dipilih.

Area di luar bagian tersebut tetap memakai material utama. Jika beberapa bagian bertumpuk, bagian yang ditambahkan terakhir mendapat prioritas.

## Menambahkan pagar dan lampu

Pilih road, lalu buka tab **Props**. Pada **QUICK ADD**, pilih:

- **Add Fence**
- **Add Street Lights**
- **Fence + Street Lights**

Setelah ditambahkan, atur sisi jalan, jarak antarobjek, dan offset pada layer props.

Untuk model pagar sendiri, pilih **Fence Prefab** pada **Advanced Prop Layers**, lalu tekan **Add / Replace Road Fence**.

### Membuat celah

Gunakan **PLACEMENT & GAPS** untuk membuka akses masuk atau menghentikan props pada bagian tertentu:

1. Pilih layer props.
2. Pilih knot di Scene View.
3. Atur **Gap Side** dan **Gap Length (m)**.
4. Tekan **Add Gap at Selected Knot**.

Anda juga dapat memakai **+ Add Gap by Distance** untuk menentukan batas **From / To (m)** secara manual.

Celah disimpan berdasarkan jarak dari awal spline. Periksa kembali batasnya setelah mengubah panjang jalan.

### Mengedit pagar tanpa mengubah jalan

1. Pilih **Path Side**.
2. Tekan **Create Editable Path for This Side**.
3. Pilih **Select / Edit Path**.
4. Geser atau putar knot pada jalur pagar.

Gunakan **+ Point Before / + Point After** untuk menambahkan titik di sekitar bagian yang ingin diedit.

Jalur ini berdiri sendiri. Perubahan bentuk jalan berikutnya tidak otomatis mengubah bentuk jalur pagar tersebut.

### Menyimpan preset props

Setelah konfigurasi selesai, tekan **Save Current as Preset**. Untuk menggunakannya pada jalan lain, pilih aset pada **Prop Preset**, lalu tekan **Apply Preset**.

Konfigurasi yang memakai jalur props dari scene perlu disimpan bersama scene atau prefab road.

## Menghubungkan jalan

Buka tab **Connections**, lalu pilih mode sesuai kebutuhan:

| Mode | Kegunaan |
|---|---|
| Join Knots (Keep Branches) | Menghubungkan titik jalan sambil mempertahankan cabang |
| Merge Splines (Endpoints) | Menyatukan dua spline terbuka dari ujungnya |

Pilih knot A dan B di Scene View, atau gunakan **Capture A / Capture B**. Periksa preview sebelum menerapkan sambungan.

Untuk sambungan antar-road, kedua jalan harus berada pada scene yang sama, memiliki lebar yang sama, belum dibake, dan mengaktifkan live updates. **Join Knots** juga memerlukan kedua road dalam keadaan aktif.

Road A menjadi acuan profil, material utama, dan konfigurasi props pada hasil gabungan. Periksa pilihan A dan B sebelum melanjutkan.

Junction dengan knot yang terhubung otomatis membersihkan permukaan yang bertumpuk, menggunakan material tanpa markah di area tengah, serta membuka celah pada pagar dan props.

Untuk jalan yang telah digabung sebelumnya, gunakan **Refresh Junction Cleanup**.

### Melepas sambungan

- Gunakan **Unlink Selected Knot** untuk melepas ikatan antar-knot.
- Gunakan **Remove Connection** untuk menghapus jalur penghubung yang dibuat oleh Join Knots.

Remove Connection mempertahankan cabang sumber di dalam container gabungan.

## Menggunakan terrain

Pada tab **Terrain**, hubungkan road dengan terrain, lalu atur penyesuaian jalan dan tanah sesuai kebutuhan.

Road Tools menyediakan snapshot terrain dasar, pengaturan clearance, dan texture bands. Terrain yang terhubung dapat mengikuti perubahan jalan saat live updates aktif.

## Menyelesaikan jalan

Saat hasil sudah siap, pilih road dan tekan **Bake Generated Meshes** pada Inspector.

Untuk melanjutkan pengeditan, tekan **Resume Editing** terlebih dahulu. Simpan scene setelah melakukan perubahan.

## Mencoba demo

Di Package Manager, pilih **Road Tools**, lalu impor sample **Demo**. Anda juga dapat menekan **Import Demo Sample** pada tab **Test Scene**.

Buka scene:

```text
Assets/Samples/Road Tools/0.1.3/Demo/Scenes/Road Tools Demo.unity
```

Demo menyertakan jalan bermarkah, terrain, lampu, pagar, serta material dan texture yang dapat dikustomisasi.

Jika impor sebelumnya belum lengkap, tekan **Import Demo Sample** kembali. File yang hilang akan ditambahkan tanpa menimpa aset yang sudah ada.

## Lokasi aset dan panduan

Road Tools menyiapkan folder berikut di proyek:

| Folder | Isi |
|---|---|
| `Assets/Road Tools/Documentation` | Panduan penggunaan |
| `Assets/Road Tools/Generated` | Mesh, prefab, profil salinan, dan preset buatan pengguna |
| `Assets/Road Tools/Development` | Scene uji yang dibuat melalui tools |
| `Assets/Samples/Road Tools/<version>/Demo` | Demo yang telah diimpor |

Simpan material, profil, dan prefab yang ingin Anda ubah di folder `Assets` proyek.

Buka **Tools > Road Tools > Open Quick Start** untuk membaca panduan penggunaan. Riwayat pembaruan tersedia di [Changelog](CHANGELOG.md).

## Batasan

- Auto Fix Clipping tidak dapat menyelesaikan semua bentuk persilangan atau belokan. Beberapa kasus memerlukan penyesuaian manual.
- Cleanup junction mendukung cabang yang terhubung melalui knot. Persilangan tanpa link, jalan bertingkat, dan sudut persimpangan dengan radius khusus belum ditangani.
- Terrain yang luas dan jumlah props yang banyak dapat memperlambat live updates.
- Periksa rentang material berbasis knot setelah menambah atau menghapus knot.

## Lisensi

Paket saat ini bertanda **UNLICENSED**. Lisensi open source belum ditentukan.