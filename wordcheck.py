# Get user input, split by word, check if each word is in the dictionary and if any isn't add that word on a new line in the dictionary

import os
def load_dictionary(file_path):
	"""Load the dictionary from a file."""
	if not os.path.exists(file_path):
		return set()
	with open(file_path, 'r', encoding='utf-8') as file:
		# Clean and lowercase each word
		return set(line.strip().lower() for line in file if line.strip())

def save_dictionary(file_path, dictionary):
	"""Save the dictionary to a file."""
	with open(file_path, 'w', encoding='utf-8') as file:
		for word in sorted(dictionary):
			file.write(f"{word}\n")
			
def clean_word(word):
    import string
    return word.strip(string.punctuation).lower()

def check_words(input_text, dictionary):
	"""Check words in the input text against the dictionary."""
	words = set(clean_word(w) for w in input_text.split())
	unknown_words = words - dictionary
	return unknown_words
	
def main():
		script_dir = os.path.dirname(os.path.realpath(__file__))
		resource_dir = os.path.join(script_dir, 'resources')
		# Load only frequency-based dictionary
		dictionary_path = os.path.join(resource_dir, 'wordfreq-en-25000.txt')
		dictionary = load_dictionary(dictionary_path)

		input_file_path = input("Enter path to text file to check: ").strip()
		if not os.path.exists(input_file_path):
			print(f"File not found: {input_file_path}")
			return
		with open(input_file_path, 'r', encoding='utf-8') as f:
			input_text = f.read()

		unknown_words = check_words(input_text, dictionary)

		if unknown_words:
			print("Unknown words found:")
			for word in unknown_words:
				print(word)
			if input("Do you want to add these words to the dictionary? (yes/no): ").strip().lower() == 'yes':
				# Add new words to the same frequency dictionary file
				dictionary.update(unknown_words)
				save_dictionary(dictionary_path, dictionary)
				print("Words added to the dictionary.")
		else:
			print("All words are known.")
		
if __name__ == "__main__":
	main()