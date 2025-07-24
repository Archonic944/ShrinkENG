# Get user input, split by word, check if each word is in the dictionary (/Users/gabriel/RiderProjects/ShrinkEng/ShrinkEng/resources/english_25k.txt) and if any isn't add that word on a new line in the dictionary

import os
def load_dictionary(file_path):
	"""Load the dictionary from a file."""
	if not os.path.exists(file_path):
		return set()
	with open(file_path, 'r', encoding='utf-8') as file:
		return set(word.strip().lower() for word in file.readlines())

def save_dictionary(file_path, dictionary):
	"""Save the dictionary to a file."""
	with open(file_path, 'w', encoding='utf-8') as file:
		for word in sorted(dictionary):
			file.write(f"{word}\n")
			
def check_words(input_text, dictionary):
	"""Check words in the input text against the dictionary."""
	words = set(input_text.lower().split())
	unknown_words = words - dictionary
	return unknown_words
	
def main():
	dictionary_path = '/Users/gabriel/RiderProjects/ShrinkEng/ShrinkEng/resources/english_25k.txt'
	dictionary = load_dictionary(dictionary_path)
	
	input_text = input("Enter text to check: ")
	unknown_words = check_words(input_text, dictionary)
	
	if unknown_words:
		print("Unknown words found:")
		for word in unknown_words:
			print(word)
		if input("Do you want to add these words to the dictionary? (yes/no): ").strip().lower() == 'yes':
			dictionary.update(unknown_words)
			save_dictionary(dictionary_path, dictionary)
			print("Words added to the dictionary.")
	else:
		print("All words are known.")