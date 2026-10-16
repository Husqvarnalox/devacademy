<script setup lang="ts">
import { ref } from "vue";

const count = ref(0);
</script>

<template>
  <main>
    <h1>{{name}}</h1>
    <p>Нажатий: {{ count }}</p>
    <button @click="count++">Увеличить</button>
  </main>
</template>
