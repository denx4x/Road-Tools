# Road Tools

**Road Tools 0.1.0** adalah plugin Unity Editor untuk membuat jalan dari Unity Splines, menempatkan props, dan menyesuaikan terrain. Mesh, collider, terrain yang terhubung, dan props mengikuti perubahan knot saat live editing aktif.

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

Untuk mengunci rilis, gunakan URL berikut **setelah tag `v0.1.0` dibuat dan di-push**:

```text
https://github.com/denx4x/Road-Tools.git#v0.1.0
```

Jika mengembangkan paket secara lokal, pilih **Add package from disk** dan buka `package.json` di folder repository ini.

## Mulai memakai Road Tools

1. Buka **Tools → Road Tools → Open Window**.
2. Pada tab **Road**, pilih Road Profile lalu tekan **Create Road From Scene View**. Untuk GameObject spline yang sudah ada, pilih objeknya dan tekan **Set Up Road Tools on Selected GameObject**.
3. Pilih knot di Scene View. Gunakan **W** untuk menggeser dan **E** untuk memutar; live update membangun ulang hasilnya.
4. Atur prefab dan penempatan pada tab **Props**. Hubungkan terrain pada tab **Terrain** untuk menyesuaikan jalan dan tanah.
5. Jika hasil sudah selesai, tekan **Bake Generated Meshes** pada Inspector road. Gunakan **Resume Editing** sebelum melanjutkan live editing.

Preset bawaan berada di `Scripts/Defaults` dalam paket dan dipakai sebagai template. Simpan profil dan aset yang ingin dikustomisasi di folder proyek `Assets`; jangan mengedit cache paket. Aset hasil generate disimpan di `Assets/Road Tools/Generated`.

## Demo opsional

Pilih **Road Tools** di Package Manager, lalu **Samples → Demo → Import**. Buka scene:

```text
Assets/Samples/Road Tools/0.1.0/Demo/Scenes/Road Tools Demo.unity
```

Demo menyertakan road bermarkah, lampu, guardrail, terrain, dan texture bands. Untuk menambah setup uji pada scene sendiri, gunakan tab **Test Scene**, lalu pilih **New Scene** atau **Current Scene**.

## Fitur

- Road Profile, mesh per chunk, collider, UV, dan bake mesh.
- Setup komponen otomatis serta live update saat knot diedit, termasuk Undo/Redo.
- Rotasi knot dan preview belokan dengan sudut serta radius yang dapat diatur.
- Auto Fix Clipping yang menyesuaikan posisi dan tangent tanpa menghapus knot.
- Prop layers dengan prefab, sisi jalan, spacing, offset, rotasi, skala, dan seed.
- Guardrail kontinu yang mengikuti kurva jalan.
- Snapshot terrain dasar, road/terrain adjustment, clearance, dan texture bands yang dapat diurutkan.
- Socket untuk menyambungkan endpoint jalan.

Auto Fix Clipping memakai perbaikan geometri yang dibatasi. Persilangan atau rute yang tidak bisa diselesaikan dalam batasnya tetap memerlukan penyesuaian manual. Terrain yang sangat besar dan props yang banyak juga memengaruhi respons live update.

## Struktur repository

```text
package.json
Scripts/            # Script jalan, terrain, props, dan template Defaults
Editor/             # Window dan alat Unity Editor
Samples~/Demo/      # Demo yang diimpor secara opsional
README.md
CHANGELOG.md
```

Repository hanya memuat paket plugin. `Library`, `Temp`, `ProjectSettings`, scene eksperimen, dan hasil generate pengguna tidak menjadi bagian paket.

Riwayat versi: [Changelog](CHANGELOG.md).

## Lisensi

Paket saat ini memakai penanda **UNLICENSED**. Lisensi open source belum dipilih.
