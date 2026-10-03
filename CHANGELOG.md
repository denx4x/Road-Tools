# Changelog

## 0.1.2 — 2026-10-03

- Memperbaiki arah hadap Road Fence bawaan pada sisi kiri, kanan, dan editable path: rail menghadap jalan, dengan tiang penyangga di belakangnya.
- Menambahkan Flip Facing pada Road Fence Model untuk mengoreksi arah model sendiri sebelum pencerminan sisi kanan, tanpa mengubah FBX atau material sumber.

- Menambahkan Local Materials per bagian road menggunakan jarak atau rentang knot, pilihan spline, preset/material khusus, preview Scene, dan handle batas Distance.
- Membagi submesh tepat pada batas material tanpa menghapus knot atau memutus UV; material utama tetap berlaku di luar section dan section terakhir mendapat prioritas pada rentang bertumpuk.
- Mendukung Undo/Redo section, perubahan material tanpa rebuild mesh/terrain/props, serta penyimpanan section dan slot material pada scene serta mesh hasil bake.
- Melindungi section dari kehilangan data saat Merge Roads; penggabungan meminta section dihapus lalu dibuat ulang karena pemetaan ulang rentang belum didukung.

- Menambahkan Point Before / After pada editable prop path dengan pemecahan kurva yang mempertahankan bentuk, pemilihan knot baru, mode Linear / Auto Smooth / Bezier, dan Undo/Redo.
- Merapikan Road Direction menjadi kartu terpisah dengan pilihan sisi, sudut preset, dan preview; meringkas material menjadi dropdown preset serta melipat pengaturan pembuatan road tambahan.
- Menambahkan empat texture dan material URP: Left Edge + Center, Right Edge + Center, Edge Lines Only, dan Unmarked Asphalt.
- Menambahkan kontrol Generate Props per road, enable per layer, dan pilihan spline yang diberi props.
- Menambahkan celah per sisi dan per spline, pemilihan celah dari knot, serta opsi menonaktifkan seluruh spline.
- Memisahkan sisi kiri dan kanan menjadi layer independen dengan salinan konfigurasi celah.
- Memotong fence FBX dan fence prosedural pada batas celah tanpa menyambungkan mesh melintasi bukaan.
- Menambahkan spline props independen yang dapat digeser dan diputar untuk membelokkan pagar tanpa mengubah road.
- Mempertahankan celah dan jalur props saat rebuild road/terrain, mendukung Undo/Redo, serta memperbarui props saat jalur independen diedit.
- Mempertahankan random transform props yang tidak terpengaruh celah, dan menyimpan kontrol penempatan pada preset.

## 0.1.1 — 2026-10-02

- Menyertakan Road Fence pada demo sample dengan prefab, FBX sumber, mesh siap pakai, material URP, dan texture yang dapat dikustomisasi.

- Membuat folder proyek Samples, Documentation, Generated, dan Development secara otomatis setelah instalasi, tanpa menimpa file pengguna.
- Menambahkan panduan QuickStart publik dan tombol untuk membuka folder proyek serta mengimpor demo; demo tetap di lokasi sample standar Unity agar pelacakan Package Manager berfungsi.
- Menempatkan scene uji baru di Assets/Road Tools/Development/Scenes dan mempertahankan scene yang sudah ada di lokasi lamanya.
- Menambahkan pilihan Road Material per road, kartu preview Profile Default dan Marked Asphalt, serta pemilihan material sendiri tanpa mengubah profil bersama.
- Menerapkan perubahan material langsung pada road live maupun hasil bake, termasuk Undo/Redo.
- Menambahkan kartu preset Add Fence, Add Street Lights, dan Fence + Street Lights dengan konfigurasi bawaan serta pembaruan layer yang sudah ada.
- Menambahkan aset Road Prop Preset, Save Current as Preset, dan Apply Preset untuk menyimpan serta memakai konfigurasi props pengguna.
- Menambahkan Merge Roads pada tab Connections untuk menggabungkan dua spline dari endpoint, dengan preview, pembalikan arah otomatis, dan validasi sambungan.
- Mempertahankan kurva, knot, data spline, serta link pada spline lain; endpoint berimpit dapat dilas dan perubahan mendukung Undo/Redo.
- Menghindari penambahan transaksi Undo saat rebuild terrain otomatis.
- Menambahkan fence model yang dibengkokkan mengikuti spline dengan subdivision, pengulangan berdasarkan panjang model, dan pemotongan segmen terakhir.
- Mempertahankan UV, material, serta mesh sumber FBX; mesh fence hasil generate dibersihkan saat rebuild.
- Menambahkan pilihan Fence Prefab dan Add / Replace Road Fence untuk mengganti layer fence lama tanpa mengubah layer props lainnya.
- Memakai jalur ketinggian fence yang dihaluskan pada terrain, menjaga penampang rail, dan menyesuaikan kaki tiang ke tanah secara terpisah.
- Memperbaiki normal fence sesuai deformasi dan memakai mesh turunan Road Fence Clean untuk menghilangkan empat sirip segitiga pada model bawaan tanpa mengubah FBX.

## 0.1.0 — 2026-10-01

- Menyiapkan Road Tools sebagai paket Unity Package Manager `com.denx4x.road-tools` untuk instalasi melalui Git URL atau package dari disk.
- Memisahkan script jalan dan alat Editor menggunakan assembly definition.
- Menempatkan template bawaan di `Scripts/Defaults` dan demo opsional di `Samples~/Demo`.
- Memisahkan aset generate pengguna dari aset paket.
- Menyertakan road mesh/collider/bake, live editing, knot rotation, road direction preview, clipping repair, prop layers, terrain adjustment, texture bands, dan intersection sockets.
- Menambahkan panduan instalasi dan penggunaan singkat di README.
