# Road Tools Quick Start

Road Tools membantu membuat jalan berbasis spline, menempatkan props, dan menyesuaikan terrain melalui Unity Editor.

## Folder proyek

Setelah paket diimpor dan Unity selesai mengompilasi, Road Tools menyiapkan folder berikut di Project:

- `Assets/Road Tools/Samples`: panduan untuk demo opsional. Impor melalui tab **Test Scene** atau **Tools > Road Tools > Import Demo Sample**; tombol memilih lokasi demo setelah impor.
- `Assets/Road Tools/Documentation`: panduan penggunaan ini.
- `Assets/Road Tools/Generated`: prefab, mesh, profil salinan, dan aset hasil generate.
- `Assets/Road Tools/Development`: scene serta aset uji yang dibuat melalui Road Tools.

Folder yang sudah ada dan perubahan pengguna tetap dipertahankan. Script plugin dan template bawaan berada di **Packages > Road Tools**. Gunakan **Tools > Road Tools > Open Project Folder** untuk memilih folder proyek.

## Membuat jalan

1. Buka **Tools > Road Tools > Open Window**.
2. Di tab **Road**, pilih **Road Profile** dan buat jalan dari Scene View. Untuk spline yang sudah ada, pilih GameObject-nya dan gunakan tombol setup Road Tools.
3. Pilih knot di Scene View. Gunakan **W** untuk menggeser dan **E** untuk memutar. Road, props, serta terrain yang terhubung mengikuti perubahan ketika live updates aktif.
4. Pilih material jalan melalui preset material atau field material khusus di tab **Road**. Simpan material yang ingin diedit di `Assets`.

## Menambahkan props

1. Pilih GameObject road, lalu buka tab **Props**.
2. Pilih preset fence atau lampu untuk menambahkan konfigurasi bawaan dan membangun props.
3. Atur sisi jalan, jarak antar prop, dan offset jika diperlukan. Prefab sendiri bisa dipilih untuk konfigurasi khusus.

Fence kontinu mengikuti bentuk spline. Model fence dengan komponen **Road Fence Model** menggunakan panjang model untuk pengulangan dan mempertahankan material model.

## Menghubungkan terrain

1. Buka tab **Terrain**, pilih terrain, lalu gunakan setup terrain manager.
2. Hubungkan road yang dipilih dengan terrain.
3. Atur penyesuaian ketinggian, clearance, dan texture bands. Snapshot terrain dasar dipakai agar penyesuaian dapat dibangun ulang tanpa menumpuk deformasi.

## Menggunakan demo

Pada tab **Test Scene**, tekan **Import Demo Sample**. Unity menempatkan demo di `Assets/Samples/Road Tools/<version>/Demo` agar Package Manager dapat melacak impor. Tombol memilih folder demo; buka `Scenes/Road Tools Demo.unity` di dalamnya saat siap. Impor demo tidak mengganti scene yang sedang dibuka.

Jika demo sudah pernah diimpor atau dipindahkan, Road Tools memilih aset yang sudah ada. Gunakan pilihan **New Scene** atau **Current Scene** untuk membuat setup uji melalui tools.

## Hasil akhir

Gunakan **Bake Generated Meshes** ketika hasil siap. Gunakan **Resume Editing** sebelum kembali mengedit spline. Profil dan material bawaan adalah template; simpan salinan yang dikustomisasi di folder proyek agar perubahan tetap tersimpan ketika paket diperbarui.
