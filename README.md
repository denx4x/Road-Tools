# Road Tools

**Road Tools 0.1.1** adalah plugin Unity Editor untuk membuat jalan dari Unity Splines, menempatkan props, dan menyesuaikan terrain. Mesh, collider, terrain yang terhubung, dan props mengikuti perubahan knot saat live editing aktif.

## Persyaratan

- Unity **6000.3**; versi target pengembangan **6000.3.6f1**.
- **Unity Splines 2.8.2**, **Mathematics 1.3.3**, dan **Universal RP 17.3.0**. Dependensi dideklarasikan di `package.json`.
- Proyek memakai **URP** dengan Render Pipeline Asset yang sudah dikonfigurasi. Material bawaan memakai shader URP Lit; memasang paket URP saja belum mengaktifkan pipeline proyek.
- Git terpasang untuk instalasi lewat Git URL.

Unity Pipeline/CLI tidak diperlukan untuk memakai plugin. Paket ini tidak menyertakan Simple Spline Road atau aset terrain dari Asset Store.

## Instalasi melalui Git URL

Setelah isi paket ini sudah di-push ke repository, buka **Window → Package Manager → Add package from Git URL**, lalu masukkan:

```text
https://github.com/denx4x/Road-Tools.git
```

Untuk mengunci rilis, gunakan URL berikut **setelah tag `v0.1.1` dibuat dan di-push**:

```text
https://github.com/denx4x/Road-Tools.git#v0.1.1
```

Jika mengembangkan paket secara lokal, pilih **Add package from disk** dan buka `package.json` di folder repository ini.

## Folder proyek setelah instalasi

Setelah Unity selesai mengompilasi paket, Road Tools otomatis menyiapkan empat folder di Project:

```text
Assets/Road Tools/
  Samples/         # Panduan menuju demo opsional
  Documentation/   # QuickStart.md untuk penggunaan tools
  Generated/       # Mesh, prefab, profil salinan, dan preset buatan pengguna
  Development/     # Scene uji baru dari Road Tools
```

Folder dan file pengguna yang sudah ada dipertahankan. Script plugin serta template berada di **Packages > Road Tools**. Gunakan **Tools > Road Tools > Open Project Folder** untuk memilih folder proyek, atau **Open Quick Start** untuk membuka panduan. Demo tetap memakai lokasi sample standar Unity agar Package Manager dapat melacaknya.

## Mulai memakai Road Tools

1. Buka **Tools → Road Tools → Open Window**.
2. Pada tab **Road**, pilih Road Profile lalu tekan **Create Road From Scene View**. Untuk GameObject spline yang sudah ada, pilih objeknya dan tekan **Set Up Road Tools on Selected GameObject**.
3. Pilih knot di Scene View. Gunakan **W** untuk menggeser dan **E** untuk memutar; live update membangun ulang hasilnya.
4. Pilih kartu material atau isi **Road Material** pada tab **Road**.
5. Tambahkan preset pada tab **Props**. Hubungkan terrain pada tab **Terrain** untuk menyesuaikan jalan dan tanah.
6. Jika hasil sudah selesai, tekan **Bake Generated Meshes** pada Inspector road. Gunakan **Resume Editing** sebelum melanjutkan live editing.

Preset bawaan berada di `Scripts/Defaults` dalam paket dan dipakai sebagai template. Simpan profil dan aset yang ingin dikustomisasi di folder proyek `Assets`; jangan mengedit cache paket. Aset hasil generate disimpan di `Assets/Road Tools/Generated`.

### Material jalan

Pada tab **Road**, kartu **Profile Default** memakai material dari Road Profile dan **Marked Asphalt** memakai asphalt bermarkah bawaan. Isi **Road Material** untuk memilih material sendiri. Jika road sedang dipilih, perubahan langsung diterapkan ke road tersebut, termasuk hasil yang sudah dibake, dan mendukung Undo/Redo. Road lain yang berbagi profil tetap memakai materialnya sendiri. Gunakan **Reset to Profile Material** untuk kembali ke material profil.

Material pada bagian pembuatan road menentukan material road berikutnya. Pilihan kosong memakai material Road Profile. Jika ingin mengedit tampilan material template, simpan salinannya di `Assets`.

### Preset props

Pilih road, lalu buka tab **Props**. Bagian **QUICK ADD** menyediakan kartu preview **Add Fence**, **Add Street Lights**, dan **Fence + Street Lights**. Satu klik menambahkan konfigurasi bawaan dan menghasilkan props. Klik berikutnya memperbarui layer bawaan yang sudah ada; layer lain dan pengaturan penempatan yang sudah dikustomisasi dipertahankan.

Untuk preset sendiri, atur **Advanced Prop Layers**, lalu tekan **Save Current as Preset**. Simpan aset preset di proyek; lokasi awal dialog adalah `Assets/Road Tools/Generated/Presets`. Pilih aset tersebut pada **Prop Preset**, lalu tekan **Apply Preset** untuk memakainya pada road lain. Layer dengan nama yang sama diperbarui, nama baru ditambahkan, dan layer lain tetap dipertahankan. Preset juga dapat dibuat melalui **Create > Road Tools > Prop Preset** pada Project.

### Road Fence

Pada tab **Props**, tekan kartu **Add Fence** untuk memakai fence bawaan. Untuk model sendiri, buka **Advanced Prop Layers**, pilih **Fence Prefab**, lalu tekan **Add / Replace Road Fence**. Tombol ini mengganti layer fence lama pada road yang dipilih, sambil mempertahankan sisi jalan dan offset yang sudah diatur. Layer props lain tetap terpisah.

Fence dengan komponen **Road Fence Model** dibengkokkan mengikuti spline; panjang model menentukan pengulangan segmen dan segmen terakhir dipotong di ujung jalan. Mesh hasil generate memakai UV serta material model, dan tinggi fence mengikuti terrain ketika grounding **Terrain** atau road conformation aktif.

**Height Smoothing Distance** pada prefab menentukan jarak penghalusan tinggi rail dalam meter. **Grounded Support Height** menentukan bagian bawah model yang menyesuaikan tanah; gunakan `0` untuk model tanpa tiang terpisah. Fence bawaan memakai rail yang dihaluskan sepanjang `4 m` dan kaki tiang hingga `0.58 m` dari dasar model. Prefab bawaan memakai mesh turunan yang dibersihkan, sementara FBX sumber tetap disimpan.

Untuk model sendiri, masukkan FBX atau prefab pada **Fence Prefab**. Road Tools membuat prefab dengan komponen **Road Fence Model** di `Assets/Road Tools/Generated/Prefabs`; importer FBX dalam `Assets` memakai **Read/Write Enabled** agar mesh dapat dibengkokkan. Atur **Longitudinal Axis** pada prefab jika panjang model mengikuti sumbu X; bawaan adalah Z. Pengaturan spacing dan random transform biasa berlaku untuk props terpisah; fence kontinu memakai panjang model dan frame spline agar sambungannya tetap bertemu.

## Demo opsional

Pilih **Road Tools** di Package Manager, lalu **Samples → Demo → Import**. Buka scene:

```text
Assets/Samples/Road Tools/0.1.1/Demo/Scenes/Road Tools Demo.unity
```

Demo menyertakan road bermarkah, lampu, guardrail, terrain, dan texture bands. Untuk menambah setup uji pada scene sendiri, gunakan tab **Test Scene**, lalu pilih **New Scene** atau **Current Scene**.

Road Fence berada di `Demo/Props/Road Fence`, dengan subfolder `Prefabs`, `Models`, `Materials`, dan `Textures`. Gunakan `Prefabs/Road Fence.prefab` untuk preset fence; model FBX sumber serta mesh siap pakai ikut disertakan. Aset sample ini bisa diedit tanpa mengubah template paket.

Demo juga dapat diimpor dari tombol **Import Demo Sample** pada tab **Test Scene**. Lokasinya mengikuti `Assets/Samples/Road Tools/<version>/Demo`; tombol memilih folder atau demo yang sudah ada tanpa mengganti scene terbuka. Scene uji baru dari **Create New Test Scene** disimpan di `Assets/Road Tools/Development/Scenes`. Scene uji yang dibuat pada versi sebelumnya tetap berada di lokasi lamanya.

## Menggabungkan dua jalan

1. Buka tab **Connections** pada window Road Tools.
2. Pilih knot ujung jalan A, lalu knot ujung jalan B di Scene View. **Capture A** dan **Capture B** bisa dipakai untuk menentukan pilihan secara manual.
3. Periksa preview sambungan, lalu tekan **Merge Roads — Sambungkan Spline**. **Swap A / B** menentukan jalan yang mempertahankan profil dan konfigurasi props.

Hasilnya satu spline pada jalan A. Kurva asal dan knot dipertahankan; endpoint yang berimpit dapat dilas menjadi satu knot bersama. Spline B yang sudah digabung dikeluarkan dari containernya, sementara spline lain dan objek buatan pengguna tetap dipertahankan. Operasi mendukung Undo/Redo.

Kedua pilihan harus endpoint dari spline terbuka yang berbeda, berada pada scene yang sama, memakai lebar jalan yang sama, belum dibake, dan mengaktifkan live updates. Knot di tengah memerlukan junction. Sambungan dengan arah berbalik atau radius terlalu sempit ditolak dengan penjelasan pada preview.

## Fitur

- Road Profile, mesh per chunk, collider, UV, dan bake mesh.
- Pilihan material per road dengan kartu preview, material sendiri, dan Undo/Redo.
- Setup komponen otomatis serta live update saat knot diedit, termasuk Undo/Redo.
- Rotasi knot dan preview belokan dengan sudut serta radius yang dapat diatur.
- Auto Fix Clipping yang menyesuaikan posisi dan tangent tanpa menghapus knot.
- Prop layers dengan prefab, sisi jalan, spacing, offset, rotasi, skala, dan seed.
- Preset props siap pakai serta penyimpanan dan penerapan preset pengguna.
- Fence model kontinu yang mengikuti kurva jalan, mempertahankan UV/material, dan menyesuaikan tinggi terrain.
- Snapshot terrain dasar, road/terrain adjustment, clearance, dan texture bands yang dapat diurutkan.
- Socket untuk menyambungkan endpoint jalan.
- Penggabungan dua spline dari endpoint, preview sambungan, pembalikan arah otomatis, dan Undo/Redo.

Auto Fix Clipping memakai perbaikan geometri yang dibatasi. Persilangan atau rute yang tidak bisa diselesaikan dalam batasnya tetap memerlukan penyesuaian manual. Terrain yang sangat besar dan props yang banyak juga memengaruhi respons live update.

## Struktur repository

```text
package.json
Scripts/            # Script jalan, terrain, props, dan template Defaults
Editor/             # Window dan alat Unity Editor
Samples~/Demo/      # Demo yang diimpor secara opsional
Documentation~/    # Template panduan publik untuk folder proyek
README.md
CHANGELOG.md
```

Repository hanya memuat paket plugin. `Library`, `Temp`, `ProjectSettings`, scene eksperimen, dan hasil generate pengguna tidak menjadi bagian paket.

Riwayat versi: [Changelog](CHANGELOG.md).

## Lisensi

Paket saat ini memakai penanda **UNLICENSED**. Lisensi open source belum dipilih.
